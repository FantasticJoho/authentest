=======================================================================
  AUTHENTEST — GUIDE DÉBUTANT
  WebAuthn : enrôlement et connexion
=======================================================================

-----------------------------------------------------------------------
PARTIE 1 — LES BASES : C'EST QUOI WEBAUTHN ?
-----------------------------------------------------------------------

WebAuthn, c'est un standard web qui permet de se connecter SANS mot de
passe, en utilisant un "authenticateur" : capteur d'empreinte, Face ID,
Windows Hello, clé USB YubiKey...

L'idée clé : ton authenticateur possède une PAIRE DE CLÉS CRYPTOGRAPHIQUES.
  - La clé PRIVÉE ne quitte JAMAIS ton appareil (elle est dans une puce sécurisée)
  - La clé PUBLIQUE est envoyée au serveur lors de l'enrôlement

Pour s'authentifier, le serveur envoie un défi (challenge), l'authenticateur
le SIGNE avec sa clé privée, et le serveur vérifie avec la clé publique.
Sans la clé privée = impossible de forger une signature = sécurité forte.


-----------------------------------------------------------------------
PARTIE 2 — ARCHITECTURE DU PROJET
-----------------------------------------------------------------------

  ┌─────────────────────────┐       HTTP + X-Api-Key       ┌──────────────────────────────┐
  │  AuthTest.Web           │ ──────────────────────────►  │  AuthTest.Api                │
  │  .NET 4.8 WebForms      │                              │  .NET 10 REST API            │
  │  Port : 8081            │ ◄──────────────────────────  │  Port : 5000                 │
  │  (ce que l'user voit)   │       JSON responses         │  (la logique + la base)      │
  └─────────────────────────┘                              └──────────────────────────────┘
           │                                                           │
           │ navigator.credentials.create/get()                       │ Fido2NetLib 4.0.1
           ▼                                                           ▼
  ┌─────────────────────────┐                              ┌──────────────────────────────┐
  │  Navigateur + OS        │                              │  Base de données InMemory    │
  │  (gère l'authenticateur)│                              │  (EF Core InMemory)          │
  └─────────────────────────┘                              └──────────────────────────────┘


-----------------------------------------------------------------------
PARTIE 3 — PARCOURS 1 : PREMIER LOGIN (ENRÔLEMENT OBLIGATOIRE)
-----------------------------------------------------------------------

Situation : un utilisateur existe en base (jonathan / Pas$word2),
mais n'a pas encore enregistré de clé WebAuthn.

ÉTAPE 1 — Vérification du nom d'utilisateur (Login.aspx)
  → WebForms envoie : POST /auth/check { username: "jonathan" }
  ← API répond   : { exists: true, hasWebAuthn: false }
  → Comme hasWebAuthn = false, on affiche le formulaire mot de passe

ÉTAPE 2 — Login par mot de passe
  → WebForms envoie : POST /auth/login { username, password }
  ← API répond   : { success: true, token: "abc123", mustChangePassword: true }
  → Le token est stocké en Session ASP.NET côté WebForms
  → Comme mustChangePassword = true → redirect vers ChangePassword.aspx

ÉTAPE 3 — Changement de mot de passe obligatoire
  → WebForms envoie : POST /auth/change-password { token, newPassword }
  ← API répond   : { success: true }
  → Redirect vers Enroll.aspx

ÉTAPE 4 — Enrôlement WebAuthn (Enroll.aspx)

  4a. Demande d'options d'enrôlement
      → WebForms envoie : POST /webauthn/register/begin { token }
      ← API répond avec un objet CredentialCreateOptions :
          {
            challenge: "aGVsbG8=",          ← challenge aléatoire (base64url)
            rp: { id: "localhost", name: "AuthTest" },
            user: { id: "...", name: "jonathan" },
            pubKeyCredParams: [{ type: "public-key", alg: -7 }]  ← algo ES256
          }
      → Ces options sont stockées en mémoire côté API (ChallengeStore)

  4b. Création de la clé par le navigateur
      → Le JS appelle : navigator.credentials.create({ publicKey: options })
      → Le navigateur demande à l'OS de créer une paire de clés
      → Windows Hello / FaceID / empreinte s'affiche
      ← L'authenticateur retourne une AuthenticatorAttestationResponse :
          - attestationObject : contient la CLÉ PUBLIQUE + preuve d'identité de l'authenticateur
          - clientDataJSON    : contient le challenge + l'origin (http://localhost:8081)
          - transports        : ["internal"] (clé interne) ou ["usb"] etc.

      IMPORTANT : le JS convertit les ArrayBuffer en base64url avant d'envoyer
                  (les bytes ne passent pas directement en JSON)

  4c. Envoi au serveur
      → WebForms envoie : POST /webauthn/register/complete
          { token, keyName: "Ma clé perso", attestationResponse: { id, rawId, type, response: {...} } }
      ← API vérifie avec Fido2NetLib :
          1. Le challenge correspond bien à celui stocké
          2. L'origin est bien "http://localhost:8081"
          3. Le rpId est bien "localhost"
          4. La signature de l'attestationObject est valide
      → Si OK, stocke en base :
          WebAuthnCredential {
            CredentialId = byte[]  ← identifiant unique de la clé
            PublicKey    = byte[]  ← clé publique COSE pour vérifier les signatures futures
            SignCount    = 0       ← compteur anti-rejeu, commence à 0
            Name         = "Ma clé perso"
          }
      ← API répond : { success: true }
      → Redirect vers Users.aspx (accès accordé)


-----------------------------------------------------------------------
PARTIE 4 — PARCOURS 2 : RECONNEXION WEBAUTHN
-----------------------------------------------------------------------

Situation : l'utilisateur revient, il a déjà une clé enrôlée.

ÉTAPE 1 — Vérification du nom d'utilisateur
  → POST /auth/check { username: "jonathan" }
  ← { exists: true, hasWebAuthn: true }
  → On affiche le bouton "Authentifier avec la clé" (pas le formulaire MDP)

ÉTAPE 2 — Demande de challenge d'authentification
  → POST /webauthn/authenticate/begin { username: "jonathan" }
  ← API répond avec AssertionOptions :
      {
        challenge: "dGVzdA==",     ← nouveau challenge aléatoire
        allowCredentials: [
          { id: "abc123...", type: "public-key" }   ← IDs des clés connues de cet user
        ]
      }
  → Stocké dans ChallengeStore, clé : "auth:jonathan"

ÉTAPE 3 — Signature par l'authenticateur
  → Le JS appelle : navigator.credentials.get({ publicKey: options })
  → Le navigateur reconnaît l'une des clés dans allowCredentials
  → Affiche Windows Hello / empreinte
  ← L'authenticateur retourne une AuthenticatorAssertionResponse :
      - authenticatorData : contient le rpIdHash + flags + signCount
      - clientDataJSON    : contient le challenge signé + l'origin
      - signature         : signature des deux précédents avec la clé privée
      - userHandle        : optionnel, identifiant de l'utilisateur

ÉTAPE 4 — Vérification côté API
  → POST /webauthn/authenticate/complete
      { username: "jonathan", assertionResponse: { id, rawId, type, response: {...} } }
  ← API :
      1. Retrouve l'utilisateur et ses credentials
      2. Trouve le credential correspondant au rawId (identifiant de la clé utilisée)
      3. Récupère les options de challenge stockées
      4. Appelle Fido2.MakeAssertionAsync() qui vérifie :
           - La SIGNATURE avec la clé publique stockée en base
           - Que le challenge correspond (anti-rejeu)
           - Que l'origin est bien dans la liste autorisée
           - Que signCount >= signCount précédent (détection clonage)
      5. Met à jour signCount en base
      6. Crée une session et retourne { success: true, token: "xyz789" }
  → Redirect vers Users.aspx


-----------------------------------------------------------------------
PARTIE 5 — POINTS D'ATTENTION DÉVELOPPÉS
-----------------------------------------------------------------------

⚠️  POINT 1 : Stockage InMemory — tout disparaît au redémarrage
    -------------------------------------------------------
    La base de données est créée en mémoire avec Entity Framework InMemory.
    C'est pratique pour un POC, mais :
      - Si tu redémarres l'API → tous les utilisateurs sont recréés à partir
        du seed (jonathan, virginie) SANS leurs clés WebAuthn
      - Leurs credentials (clés publiques) sont perdus
      - Il faudra ré-enrôler les clés WebAuthn à chaque redémarrage API
    
    Pour la production, il faudra remplacer UseInMemoryDatabase("AuthTestDb")
    par une vraie base SQL (PostgreSQL, SQL Server, etc.)


⚠️  POINT 2 : ChallengeStore en mémoire — une seule instance
    -------------------------------------------------------
    Les challenges (nonces) sont stockés dans un Dictionary en mémoire C#
    (classe ChallengeStore, Singleton injecté par DI).
    
    Problème si tu deploies sur plusieurs serveurs (load balancing) :
      - Le begin peut être traité par le serveur A (qui stocke le challenge)
      - Le complete peut arriver sur le serveur B (qui ne connaît pas le challenge)
      - → Erreur "No pending challenge"
    
    Pour la production : utiliser Redis ou une table SQL pour les challenges.
    
    Autre subtilité : si l'utilisateur clique deux fois sur "Enregistrer une clé",
    le deuxième begin écrase le challenge du premier. Le premier tentative sera
    alors invalide. C'est acceptable en POC.


⚠️  POINT 3 : Sessions WebForms — pas de vraie authentification côté serveur
    -------------------------------------------------------
    Le token renvoyé par l'API (une chaîne aléatoire) est stocké dans la
    Session ASP.NET (mémoire serveur IIS Express).
    
    SessionHelper.SessionToken = result["token"]?.ToString();
    
    Ce token est renvoyé à chaque appel API pour identifier l'utilisateur.
    Côté API, le SessionStore vérifie ce token.
    
    ⚡ Risque : si le serveur WebForms redémarre, la session est perdue et
    l'utilisateur doit se reconnecter. C'est normal pour un POC.


⚠️  POINT 4 : Fido2NetLib 4.0.1 — noms de propriétés différents des versions récentes
    -------------------------------------------------------
    La librairie a changé ses noms entre versions :
    
    Version ancienne (< 4.0)      Version 4.0.1 (ce projet)
    RPID                    →     ServerDomain
    RPName                  →     ServerName
    
    Si tu lis de la documentation ou des exemples en ligne écrits pour
    une autre version, les noms seront différents → erreur de compilation.
    
    Toujours vérifier la version du package dans AuthTest.Api.csproj :
    <PackageReference Include="Fido2" Version="4.0.1" />


⚠️  POINT 5 : Désérialisation JSON — problème de model binding ASP.NET Core
    -------------------------------------------------------
    Normalement, [FromBody] dans un controller ASP.NET Core désérialise
    automatiquement le JSON du body de la requête.
    
    Mais AuthenticatorAssertionRawResponse (type Fido2NetLib) contient
    des tableaux de bytes encodés en base64url. Le désérialiseur par défaut
    de System.Text.Json ne sait pas les décoder correctement depuis
    du JSON "classique" envoyé par JavaScriptSerializer (côté WebForms).
    
    Solution dans ce projet :
      - On reçoit le body comme JsonElement (type générique)
      - On refait une désérialisation manuelle avec PropertyNameCaseInsensitive = true
      - Fido2NetLib gère lui-même le décodage base64url interne
    
    Concrètement dans WebAuthnController.cs :
    
      // Au lieu de :
      public record AuthCompleteRequest(string Username, AuthenticatorAssertionRawResponse AssertionResponse);
      
      // On fait :
      public record AuthCompleteRequest(string Username, JsonElement AssertionResponse);
      // puis dans la méthode :
      var assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(
          req.AssertionResponse.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });


⚠️  POINT 6 : L'origin et le rpId doivent correspondre EXACTEMENT
    -------------------------------------------------------
    WebAuthn est conçu pour empêcher le phishing. Le navigateur vérifie
    automatiquement que :
      - La page qui appelle navigator.credentials est bien sur l'origin déclarée
      - Le rpId dans les options correspond au domaine de la page
    
    Dans ce projet :
      - Origin = "http://localhost:8081" (port WebForms)
      - RpId   = "localhost"
    
    Si tu accèdes à la page via "http://127.0.0.1:8081" au lieu de
    "http://localhost:8081", WebAuthn refusera car l'origin ne correspond pas.
    
    Ces valeurs sont configurées dans appsettings.json :
      "Fido2": {
        "ServerDomain": "localhost",
        "ServerName": "AuthTest",
        "Origins": ["http://localhost:8081"]
      }


⚠️  POINT 7 : Le SignCount — protection contre la copie de clé
    -------------------------------------------------------
    Chaque fois que tu utilises ta clé WebAuthn, le compteur SignCount
    est incrémenté. Le serveur stocke la dernière valeur vue.
    
    Si quelqu'un copie physiquement ta clé (clone), l'autre appareil
    aura un SignCount identique ou inférieur → le serveur détecte une anomalie.
    
    Dans ce POC, le SignCount est stocké dans WebAuthnCredential.SignCount
    et mis à jour après chaque authentification réussie.
    
    Note : certains authenticateurs (ex: Windows Hello) retournent toujours
    SignCount = 0, ce qui désactive cette protection. C'est un choix de Microsoft.


⚠️  POINT 8 : Encodage des caractères accentués (bug corrigé)
    -------------------------------------------------------
    Les pages .aspx en .NET Framework 4.8 peuvent être mal encodées
    si l'en-tête HTTP ne précise pas UTF-8.
    
    Double correction appliquée :
    
    1. Web.config — demander à ASP.NET d'utiliser UTF-8 pour toutes les réponses :
       <globalization requestEncoding="utf-8" responseEncoding="utf-8" fileEncoding="utf-8" />
    
    2. Chaque page .aspx — informer le navigateur d'interpréter en UTF-8 :
       <meta charset="utf-8" />
    
    Sans ces deux lignes, "clé" s'affichait "clÃ©" car le navigateur
    interprétait les bytes UTF-8 comme du Latin-1 (ISO-8859-1).


-----------------------------------------------------------------------
PARTIE 6 — RÉSUMÉ EN UNE PHRASE PAR CONCEPT
-----------------------------------------------------------------------

  Challenge      = question secrète unique que seul ton authenticateur peut répondre
  CredentialId   = identifiant de ta clé (comme un numéro de carte)
  PublicKey      = cadenas que le serveur garde (ne sert qu'à vérifier)
  PrivateKey     = clé secrète dans ton appareil (ne sort JAMAIS)
  SignCount      = compteur pour détecter si ta clé a été copiée
  RpId           = nom de domaine du site (localhost ici)
  Origin         = adresse complète du site (http://localhost:8081)
  Token          = badge temporaire après login réussi, valide pour cette session

=======================================================================