# AUTHENTEST — GUIDE DÉBUTANT
## WebAuthn : enrôlement et connexion

---

## PARTIE 1 — LES BASES : C'EST QUOI WEBAUTHN ?

WebAuthn, c'est un standard web qui permet de se connecter SANS mot de
passe, en utilisant un "authenticateur" : capteur d'empreinte, Face ID,
Windows Hello, clé USB YubiKey...

L'idée clé : ton authenticateur possède une PAIRE DE CLÉS CRYPTOGRAPHIQUES.
- La clé **PRIVÉE** ne quitte JAMAIS ton appareil (elle est dans une puce sécurisée)
- La clé **PUBLIQUE** est envoyée au serveur lors de l'enrôlement

Pour s'authentifier, le serveur envoie un défi (challenge), l'authenticateur
le SIGNE avec sa clé privée, et le serveur vérifie avec la clé publique.
Sans la clé privée = impossible de forger une signature = sécurité forte.

---

## PARTIE 2 — ARCHITECTURE DU PROJET

```mermaid
graph LR
    WF["**AuthTest.Web**\n.NET 4.8 WebForms\nPort : 8081\n(ce que l'user voit)"]
    API["**AuthTest.Api**\n.NET 10 REST API\nPort : 5000\n(la logique + la base)"]
    BR["**Navigateur + OS**\n(gère l'authenticateur)"]
    DB["**Base de données**\nEF Core InMemory"]

    WF -->|"HTTP + X-Api-Key"| API
    API -->|"JSON responses"| WF
    WF -->|"navigator.credentials.create/get()"| BR
    API -->|"Fido2 4.0.1"| DB
```

---

## PARTIE 3 — PARCOURS 1 : PREMIER LOGIN (ENRÔLEMENT OBLIGATOIRE)

Situation : un utilisateur existe en base (jonathan / Pas$word2),
mais n'a pas encore enregistré de clé WebAuthn.

```mermaid
sequenceDiagram
    actor U as Utilisateur
    participant W as Login.aspx (WebForms)
    participant A as API .NET 10
    participant DB as Base de données
    participant OS as Navigateur / OS

    U->>W: Saisit son nom d'utilisateur
    W->>A: POST /auth/check { username }
    A-->>W: { exists: true, hasWebAuthn: false }
    W->>U: Affiche le formulaire mot de passe

    U->>W: Saisit son mot de passe
    W->>A: POST /auth/login { username, password }
    A-->>W: { success: true, mustChangePassword: true }
    W->>W: Stocke username en Session ASP.NET
    W->>U: Redirige vers ChangePassword.aspx

    U->>W: Saisit nouveau mot de passe
    W->>A: POST /auth/change-password { username, newPassword }
    A->>DB: Met à jour le hash + mustChangePassword = false
    A-->>W: { success: true }
    W->>U: Redirige vers Enroll.aspx

    Note over W,A: Enrôlement WebAuthn (begin → complete)

    W->>A: POST /webauthn/register/begin { username, rpId }
    A->>DB: Stocke challenge dans ChallengeStore (TTL 2 min)
    A-->>W: CredentialCreateOptions { challenge, rp, user, ... }

    W->>OS: navigator.credentials.create({ publicKey: options })
    OS->>U: Windows Hello / FaceID / empreinte
    U->>OS: Valide avec biométrie
    OS-->>W: AuthenticatorAttestationResponse { attestationObject, clientDataJSON }

    W->>A: POST /webauthn/register/complete { username, keyName, attestationResponse, rpId }
    A->>DB: Récupère et supprime le challenge (vérifie TTL)
    Note over A: Fido2.MakeNewCredentialAsync() vérifie :<br/>challenge, origin, rpId, signature
    A->>DB: Stocke WebAuthnCredential { CredentialId, PublicKey, SignCount=0, Name }
    A-->>W: { success: true }
    W->>U: Redirige vers Users.aspx
```

### Détail de l'étape d'enrôlement (4c)

L'API vérifie avec Fido2 :
1. Le challenge correspond bien à celui stocké en base
2. L'origin est bien `http://localhost:8081`
3. Le rpId est bien `localhost`
4. La signature de l'attestationObject est valide

Si OK, stocke en base :
```
WebAuthnCredential {
  CredentialId = byte[]   ← identifiant unique de la clé
  PublicKey    = byte[]   ← clé publique COSE pour vérifier les signatures futures
  SignCount    = 0        ← compteur anti-rejeu, commence à 0
  Name         = "Ma clé perso"
}
```

> **IMPORTANT** : le JS convertit les ArrayBuffer en base64url avant d'envoyer
> (les bytes ne passent pas directement en JSON)

---

## PARTIE 4 — PARCOURS 2 : RECONNEXION WEBAUTHN

Situation : l'utilisateur revient, il a déjà une clé enrôlée.

```mermaid
sequenceDiagram
    actor U as Utilisateur
    participant W as Login.aspx (WebForms)
    participant A as API .NET 10
    participant DB as Base de données
    participant OS as Navigateur / OS

    U->>W: Saisit son nom d'utilisateur
    W->>A: POST /auth/check { username }
    A-->>W: { exists: true, hasWebAuthn: true }
    W->>U: Affiche le bouton "Authentifier avec la clé"

    U->>W: Clique sur "Authentifier avec la clé"
    W->>A: POST /webauthn/authenticate/begin { username }
    A->>DB: Stocke AssertionOptions dans ChallengeStore, clé "auth:jonathan" (TTL 2 min)
    A-->>W: AssertionOptions { challenge, allowCredentials: [{ id: credId }] }

    W->>OS: navigator.credentials.get({ publicKey: options })
    OS->>U: Windows Hello / FaceID / empreinte
    U->>OS: Valide avec biométrie
    OS-->>W: AuthenticatorAssertionResponse { authenticatorData, clientDataJSON, signature }

    W->>A: POST /webauthn/authenticate/complete { username, assertionResponse }
    A->>DB: Récupère et supprime le challenge (vérifie TTL)
    A->>DB: Trouve le credential par rawId
    Note over A: Fido2.MakeAssertionAsync() vérifie :<br/>signature, challenge, origin, signCount
    A->>DB: Met à jour SignCount
    A-->>W: { success: true }
    W->>W: Marque la session front comme enrôlée
    W->>U: Redirige vers Users.aspx
```

### Ce que vérifie MakeAssertionAsync

1. La **SIGNATURE** avec la clé publique stockée en base
2. Que le **challenge** correspond (anti-rejeu)
3. Que l'**origin** est bien dans la liste autorisée
4. Que **signCount** >= signCount précédent (détection clonage)

---

## PARTIE 5 — COMMENT FIDO2NETLIB GÈRE LES CHALLENGES

Fido2 est la bibliothèque C# qui implémente le protocole WebAuthn côté serveur.
Elle expose 4 méthodes principales, chacune liée à une étape du protocole.

```mermaid
graph TD
    subgraph Enrôlement
        A1["RequestNewCredential()"] -->|"génère challenge aléatoire\ncrée CredentialCreateOptions"| A2["→ stocké dans ChallengeStore"]
        A3["MakeNewCredentialAsync()"] -->|"reçoit attestationResponse\nvérifie challenge + signature"| A4["→ stocke PublicKey en base"]
    end

    subgraph Authentification
        B1["GetAssertionOptions()"] -->|"génère challenge aléatoire\ncrée AssertionOptions"| B2["→ stocké dans ChallengeStore"]
        B3["MakeAssertionAsync()"] -->|"reçoit assertionResponse\nvérifie challenge + signature"| B4["→ met à jour SignCount"]
    end
```

### RequestNewCredential() — Enrôlement, étape 1

```csharp
var options = _fido2.RequestNewCredential(new RequestNewCredentialParams
{
    User = fidoUser,                        // { Id, Name, DisplayName }
    ExcludeCredentials = existingKeys,      // clés déjà enrôlées (pour éviter les doublons)
    AuthenticatorSelection = new AuthenticatorSelection
    {
        AuthenticatorAttachment = AuthenticatorAttachment.Platform,  // capteur interne uniquement
        UserVerification = UserVerificationRequirement.Preferred
    },
    AttestationPreference = AttestationConveyancePreference.None
});
// options contient un challenge aléatoire (byte[]) → à stocker dans ChallengeStore
// options est envoyé au navigateur qui l'utilise pour appeler navigator.credentials.create()
```

### MakeNewCredentialAsync() — Enrôlement, étape 2

```csharp
var makeResult = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
{
    AttestationResponse = req.AttestationResponse,   // réponse du navigateur
    OriginalOptions = storedOptions,                  // options récupérées du ChallengeStore
    IsCredentialIdUniqueToUserCallback = isUniqueCallback
});
// makeResult.Id        → identifiant de la nouvelle clé (CredentialId)
// makeResult.PublicKey → clé publique COSE à stocker en base
```

Fido2 vérifie en interne :
- Que `clientDataJSON.challenge` == challenge dans `OriginalOptions` → **anti-rejeu**
- Que `clientDataJSON.origin` est dans la liste des origins autorisées → **anti-phishing**
- Que `clientDataJSON.type` == `"webauthn.create"`
- Que la signature de l'`attestationObject` est valide

### GetAssertionOptions() — Authentification, étape 1

```csharp
var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
{
    AllowedCredentials = allowedKeys,   // liste des CredentialId de cet utilisateur
    UserVerification = UserVerificationRequirement.Preferred
});
// options contient un NOUVEAU challenge aléatoire → à stocker dans ChallengeStore
// options est envoyé au navigateur qui l'utilise pour appeler navigator.credentials.get()
```

### MakeAssertionAsync() — Authentification, étape 2

```csharp
var result = await _fido2.MakeAssertionAsync(new MakeAssertionParams
{
    AssertionResponse = assertionResponse,         // réponse du navigateur
    OriginalOptions = storedOptions,               // options récupérées du ChallengeStore
    StoredPublicKey = credential.PublicKey,        // clé publique stockée en base
    StoredSignatureCounter = credential.SignCount, // compteur stocké en base
    IsUserHandleOwnerOfCredentialIdCallback = isUserOwner
});
// result.SignCount → nouveau compteur à sauvegarder en base
```

Fido2 vérifie en interne :
- Que `clientDataJSON.challenge` == challenge dans `OriginalOptions` → **anti-rejeu**
- Que `clientDataJSON.origin` est dans la liste autorisée → **anti-phishing**
- Que `clientDataJSON.type` == `"webauthn.get"`
- Que la **signature** dans `assertionResponse` est valide avec `StoredPublicKey`
- Que `signCount > StoredSignatureCounter` (sauf si signCount == 0) → **anti-clonage**

### Pourquoi le ChallengeStore est indispensable avec Fido2

```mermaid
sequenceDiagram
    participant WC as WebAuthnController
    participant F2 as Fido2
    participant CS as ChallengeStore (EF InMemory)
    participant BR as Navigateur

    Note over WC,CS: /authenticate/begin
    WC->>F2: GetAssertionOptions(allowedKeys)
    F2-->>WC: AssertionOptions { challenge: "xyz" }
    WC->>CS: StoreAssertOptionsAsync("auth:jonathan", options, TTL=2min)

    Note over WC,CS: /authenticate/complete (quelques secondes plus tard)
    BR->>WC: assertionResponse (contient signature de "xyz")
    WC->>CS: TakeAssertOptionsAsync("auth:jonathan")
    CS-->>WC: AssertionOptions { challenge: "xyz" }
    WC->>F2: MakeAssertionAsync(assertionResponse, storedOptions, publicKey, signCount)
    Note over F2: compare challenge "xyz" de storedOptions<br/>avec challenge dans assertionResponse.clientDataJSON<br/>→ si différent : ERREUR (protection anti-rejeu)
    F2-->>WC: AssertionVerificationResult { SignCount }
```

Le challenge est généré côté serveur (pas côté client) pour garantir
que c'est bien le serveur qui contrôle ce qui est signé.
Un attaquant ne peut pas réutiliser une ancienne signature car le challenge
change à chaque fois.

---

## PARTIE 6 — POINTS D'ATTENTION DÉVELOPPÉS

### ⚠️ POINT 1 : Stockage InMemory — tout disparaît au redémarrage

La base de données est créée en mémoire avec Entity Framework InMemory.
C'est pratique pour un POC, mais :
- Si tu redémarres l'API → tous les utilisateurs sont recréés à partir
  du seed (jonathan, virginie) SANS leurs clés WebAuthn
- Leurs credentials (clés publiques) sont perdus
- Il faudra ré-enrôler les clés WebAuthn à chaque redémarrage API

Pour la production, il faudra remplacer `UseInMemoryDatabase("AuthTestDb")`
par une vraie base SQL (PostgreSQL, SQL Server, etc.)


### ⚠️ POINT 2 : ChallengeStore — vestiaire temporaire des défis cryptographiques

WebAuthn fonctionne en deux allers-retours (begin → complete).
Entre les deux, le serveur doit mémoriser le challenge qu'il a généré,
pour pouvoir vérifier la réponse du navigateur.

La table `WebAuthnChallenge` contient :
- `Key`         : identifiant du challenge (ex: token de session, `"auth:jonathan"`)
- `Type`        : `"register"` (enrôlement) ou `"assert"` (authentification)
- `OptionsJson` : les options complètes sérialisées en JSON
- `ExpiresAt`   : date d'expiration (2 minutes après création)

**TTL — Durée de vie de 2 minutes**

Chaque challenge expire automatiquement après 2 minutes (TTL = Time To Live).
Pourquoi c'est important :
- Si quelqu'un intercepte le challenge, il ne peut pas l'utiliser plus tard
- L'utilisateur a 2 minutes pour poser son doigt sur le capteur
- À chaque `Store*`, les lignes expirées sont supprimées automatiquement

```csharp
// Stocker un challenge (TTL 2 min)
db.Challenges.Add(new WebAuthnChallenge {
    Key = tokenSession,
    Type = "register",
    OptionsJson = options.ToJson(),
    ExpiresAt = DateTime.UtcNow.AddMinutes(2)   // le TTL
});

// Récupérer et consommer un challenge
var row = db.Challenges.FirstOrDefault(
    c => c.Key == key && c.ExpiresAt > DateTime.UtcNow  // filtre TTL
);
if (row is null) return BadRequest("No pending challenge"); // expiré ou inexistant
db.Challenges.Remove(row);  // consommé → ne peut plus être réutilisé
```

**Comportement Scoped (pas Singleton)**

`ChallengeStore` est enregistré en `Scoped` dans la DI (pas `Singleton`),
parce qu'il dépend de `AppDbContext` qui est lui-même Scoped.
Cela signifie qu'une nouvelle instance est créée à chaque requête HTTP.

Pour le multi-instance en production : remplacer `UseInMemoryDatabase` par
un vrai SQL (Oracle, PostgreSQL...). La logique du ChallengeStore
n'aurait PAS à changer, seul le provider EF change.

Autre subtilité : si l'utilisateur clique deux fois sur "Enregistrer une clé",
le deuxième begin écrase le challenge du premier (l'ancien est supprimé).
C'est acceptable en POC.


### ⚠️ POINT 3 : Session front uniquement (API stateless utilisateur)

La session ASP.NET WebForms stocke le contexte UX (`Username`, `IsEnrolled`).
L'API fonctionne en mode stateless pour l'état utilisateur.

Exemple côté front :

```csharp
SessionHelper.CurrentUsername = username;
SessionHelper.IsEnrolled = true;
```

Les appels API portent l'identité fonctionnelle nécessaire dans le body
(`username`, `rpId`) et les challenges WebAuthn restent gérés avec TTL court
dans `ChallengeStore`.

> ⚡ Risque : si le serveur WebForms redémarre, la session front est perdue et
> l'utilisateur doit se reconnecter. C'est normal pour un POC.


### ⚠️ POINT 4 : Fido2NetLib 4.0.1 — noms de propriétés différents des versions récentes

La librairie a changé ses noms entre versions :

| Version ancienne (< 4.0) | Version 4.0.1 (ce projet) |
|--------------------------|---------------------------|
| `RPID`                   | `ServerDomain`            |
| `RPName`                 | `ServerName`              |

Si tu lis de la documentation ou des exemples en ligne écrits pour
une autre version, les noms seront différents → erreur de compilation.

Toujours vérifier la version du package dans `AuthTest.Api.csproj` :
```xml
<PackageReference Include="Fido2" Version="4.0.1" />
```


### ⚠️ POINT 5 : Désérialisation JSON — problème de model binding ASP.NET Core

Normalement, `[FromBody]` dans un controller ASP.NET Core désérialise
automatiquement le JSON du body de la requête.

Mais `AuthenticatorAssertionRawResponse` (type Fido2) contient
des tableaux de bytes encodés en base64url. Le désérialiseur par défaut
de `System.Text.Json` ne sait pas les décoder correctement depuis
du JSON "classique" envoyé par `JavaScriptSerializer` (côté WebForms).

Solution dans ce projet :
- On reçoit le body comme `JsonElement` (type générique)
- On refait une désérialisation manuelle avec `PropertyNameCaseInsensitive = true`
- Fido2 gère lui-même le décodage base64url interne

```csharp
// Au lieu de :
public record AuthCompleteRequest(string Username, AuthenticatorAssertionRawResponse AssertionResponse);

// On fait :
public record AuthCompleteRequest(string Username, JsonElement AssertionResponse);
// puis dans la méthode :
var assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(
    req.AssertionResponse.GetRawText(),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
```


### ⚠️ POINT 6 : L'origin et le rpId doivent correspondre EXACTEMENT

WebAuthn est conçu pour empêcher le phishing. Le navigateur vérifie
automatiquement que :
- La page qui appelle `navigator.credentials` est bien sur l'origin déclarée
- Le rpId dans les options correspond au domaine de la page

Dans ce projet :
- Origin = `http://localhost:8081` (port WebForms)
- RpId   = `localhost`

Si tu accèdes à la page via `http://127.0.0.1:8081` au lieu de
`http://localhost:8081`, WebAuthn refusera car l'origin ne correspond pas.

Ces valeurs sont configurées dans `appsettings.json` :
```json
"Fido2": {
  "ServerDomain": "localhost",
  "ServerName": "AuthTest",
  "Origins": ["http://localhost:8081"]
}
```


### ⚠️ POINT 7 : Le SignCount — protection contre la copie de clé

Chaque fois que tu utilises ta clé WebAuthn, le compteur `SignCount`
est incrémenté. Le serveur stocke la dernière valeur vue.

Si quelqu'un copie physiquement ta clé (clone), l'autre appareil
aura un `SignCount` identique ou inférieur → le serveur détecte une anomalie.

Dans ce POC, le `SignCount` est stocké dans `WebAuthnCredential.SignCount`
et mis à jour après chaque authentification réussie.

> Note : certains authenticateurs (ex: Windows Hello) retournent toujours
> `SignCount = 0`, ce qui désactive cette protection. C'est un choix de Microsoft.


### ⚠️ POINT 8 : Encodage des caractères accentués (bug corrigé)

Les pages `.aspx` en .NET Framework 4.8 peuvent être mal encodées
si l'en-tête HTTP ne précise pas UTF-8.

Double correction appliquée :

1. `Web.config` — demander à ASP.NET d'utiliser UTF-8 pour toutes les réponses :
```xml
<globalization requestEncoding="utf-8" responseEncoding="utf-8" fileEncoding="utf-8" />
```

2. Chaque page `.aspx` — informer le navigateur d'interpréter en UTF-8 :
```html
<meta charset="utf-8" />
```

Sans ces deux lignes, `"clé"` s'affichait `"clÃ©"` car le navigateur
interprétait les bytes UTF-8 comme du Latin-1 (ISO-8859-1).

---

## PARTIE 7 — VUE D'ENSEMBLE : ENDPOINTS, SERVICES ET TABLES

Diagramme complet des 4 flux — chaque flèche indique l'endpoint appelé,
la méthode de service ou la table EF touchée.

```mermaid
sequenceDiagram
    actor U as Utilisateur
    participant W as WebForms
    participant A as API Endpoint
    participant S as Service C#
    participant DB as Table EF InMemory

    rect rgb(230, 240, 255)
        Note over U,DB: LOGIN MOT DE PASSE

        U->>W: Saisit username
        W->>A: POST /auth/check
        A->>DB: Users (SELECT WHERE Username)
        A->>DB: Credentials (COUNT WHERE UserId)
        A-->>W: { exists, hasWebAuthn, mustChangePassword }

        U->>W: Saisit mot de passe
        W->>A: POST /auth/login
        A->>DB: Users + Credentials (SELECT)
        Note over A: BCrypt.Verify(password, PasswordHash)
        A-->>W: { success, mustChangePassword }

        U->>W: Saisit nouveau mot de passe
        W->>A: POST /auth/change-password
        A->>DB: Users (UPDATE PasswordHash, MustChangePassword=false)
        A-->>W: { success }
    end

    rect rgb(230, 255, 230)
        Note over U,DB: ENRÔLEMENT WEBAUTHN

        W->>A: POST /webauthn/register/begin
        A->>DB: Users (SELECT WHERE Username)
        A->>DB: Credentials (SELECT WHERE UserId — liste d'exclusion)
        Note over A: Fido2.RequestNewCredential(user, excludeList)
        A->>S: ChallengeStore.StoreRegisterOptionsAsync("reg:username", options)
        A->>DB: WebAuthnChallenges (INSERT Key="reg:username", Type="register", ExpiresAt=+2min)
        A-->>W: CredentialCreateOptions { challenge, rp, user, pubKeyCredParams }

        W->>U: navigator.credentials.create(options)
        U-->>W: AttestationResponse { attestationObject, clientDataJSON }

        W->>A: POST /webauthn/register/complete
        A->>S: ChallengeStore.TakeRegisterOptionsAsync("reg:username")
        A->>DB: WebAuthnChallenges (SELECT WHERE Key="reg:username" AND ExpiresAt>now, puis DELETE)
        Note over A: Fido2.MakeNewCredentialAsync(attestationResponse, storedOptions)<br/>vérifie challenge + origin + rpId + signature
        A->>DB: Credentials (INSERT CredentialId, PublicKey, SignCount=0, Name, UserId)
        A-->>W: { success }
    end

    rect rgb(255, 245, 220)
        Note over U,DB: RECONNEXION WEBAUTHN

        W->>A: POST /webauthn/authenticate/begin
        A->>DB: Users + Credentials (SELECT WHERE Username)
        Note over A: Fido2.GetAssertionOptions(allowedKeys)
        A->>S: ChallengeStore.StoreAssertOptionsAsync("auth:username", options)
        A->>DB: WebAuthnChallenges (INSERT Key="auth:username", Type="assert", ExpiresAt=+2min)
        A-->>W: AssertionOptions { challenge, allowCredentials }

        W->>U: navigator.credentials.get(options)
        U-->>W: AssertionResponse { authenticatorData, clientDataJSON, signature }

        W->>A: POST /webauthn/authenticate/complete
        A->>DB: Users + Credentials (SELECT WHERE Username)
        A->>S: ChallengeStore.TakeAssertOptionsAsync("auth:username")
        A->>DB: WebAuthnChallenges (SELECT WHERE Key AND ExpiresAt>now, puis DELETE)
        Note over A: Fido2.MakeAssertionAsync(assertionResponse, storedOptions, publicKey, signCount)<br/>vérifie signature + challenge + origin + signCount
        A->>DB: Credentials (UPDATE SignCount)
        A-->>W: { success }
    end

    rect rgb(255, 225, 225)
        Note over U,DB: RESET 2FA

        W->>A: POST /users/{id}/reset-2fa
        A->>DB: Users + Credentials (SELECT WHERE Id)
        A->>DB: Credentials (DELETE WHERE UserId)
        A-->>W: { success }
        Note over W,DB: Prochain /auth/check → hasWebAuthn=false<br/>Le flow "Premier login" redémarre
    end
```

---

## PARTIE 8 — RÉSUMÉ EN UNE PHRASE PAR CONCEPT

| Concept        | Définition |
|----------------|-----------|
| Challenge       | Question secrète unique que seul ton authenticateur peut répondre |
| ChallengeStore  | Service qui mémorise temporairement le challenge entre /begin et /complete (TTL 2 min) |
| CredentialId    | Identifiant de ta clé (comme un numéro de carte) |
| PublicKey       | Cadenas que le serveur garde (ne sert qu'à vérifier) |
| PrivateKey      | Clé secrète dans ton appareil (ne sort JAMAIS) |
| SignCount       | Compteur pour détecter si ta clé a été copiée |
| RpId            | Nom de domaine du site (ex: `localhost` ou `test.joho`) |
| Origin          | Adresse complète du site (ex: `https://localhost:8081`) |
| Fido2     | Bibliothèque C# qui implémente le protocole WebAuthn côté serveur |
