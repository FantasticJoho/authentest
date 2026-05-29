## Plan: POC Authentification WebForms 4.8 + API .NET 10

### 1. Architecture cible (important)
1. Le scope est sur **2 applications séparées**, pas un seul projet.
2. Frontend: WebForms .NET Framework 4.8.
3. Backend: API .NET 10.
4. Les appels Front -> API sont sécurisés par header `X-Api-Key` fixe (POC).
5. CORS activé pour ports locaux distincts.
6. Données en mémoire avec EF InMemory.
7. Pas d ASP.NET Identity.

### 2. Structure de workspace attendue
1. `AuthTest.Web` (WebForms 4.8)
2. `AuthTest.Api` (.NET 10 Web API)
3. `AuthTest.http` (tests manuels endpoints)
4. `AGENT_TRACKING.md` (suivi compact unique)

### 3. Règles métier clés
1. On peut créer un utilisateur avec username + mot de passe.
2. `auth/check` renvoie `hasWebAuthn`.
3. Si `hasWebAuthn=false` (nouveau compte ou après reset 2FA):
   1. Login mot de passe autorisé.
   2. Changement de mot de passe obligatoire.
   3. Enrôlement WebAuthn obligatoire.
   4. Nom de clé obligatoire à l enrôlement.
   5. Aucun accès applicatif avant enrôlement terminé.
4. Si `hasWebAuthn=true`:
   1. Proposer WebAuthn.
   2. Authentifier via WebAuthn.
5. POC sans RBAC:
   1. Tout utilisateur authentifié peut lister les users.
   2. Tout utilisateur authentifié peut reset le 2FA de n importe quel user.
6. Le modèle doit être prêt pour multi-clés dès le lot 1:
   1. `User` 1->N `WebAuthnCredential`.
   2. `WebAuthnCredential.Name` obligatoire.


### mermaid workflow

sequenceDiagram
    autonumber
    participant U as Utilisateur
    participant W as WebForms 4.8
    participant A as API .NET 10
    participant D as EF InMemory
    participant P as Plateforme WebAuthn

    Note over U,W: Parcours 1 - Création utilisateur
    U->>W: Créer compte (username + password)
    W->>A: POST /users
    A->>D: Insert User (MustChangePassword=true)
    D-->>A: OK
    A-->>W: 201 Created

    Note over U,W: Parcours 2 - Première connexion sans 2FA
    U->>W: Saisir username
    W->>A: POST /auth/check
    A->>D: Lire user + credentials
    D-->>A: hasWebAuthn=false
    A-->>W: exists=true, hasWebAuthn=false

    U->>W: Saisir mot de passe
    W->>A: POST /auth/login
    A->>D: Vérifier hash BCrypt
    D-->>A: OK, mustChangePassword=true
    A-->>W: login success

    U->>W: Changer mot de passe
    W->>A: POST /auth/change-password
    A->>D: Update hash + MustChangePassword=false
    D-->>A: OK
    A-->>W: success

    U->>W: Saisir nom de clé + enrôler
    W->>A: POST /webauthn/register/begin
    A-->>W: CredentialCreateOptions
    W->>P: navigator.credentials.create(...)
    P-->>W: Attestation
    W->>A: POST /webauthn/register/complete (attestation + keyName)
    A->>D: Insert WebAuthnCredential(Name=keyName)
    D-->>A: OK
    A-->>W: success
    W-->>U: Accès Users.aspx autorisé

    Note over U,W: Parcours 3 - POC sans RBAC
    U->>W: Ouvrir liste utilisateurs
    W->>A: GET /users
    A->>D: Read all users
    D-->>A: users
    A-->>W: users
    W-->>U: Liste affichée

    U->>W: Reset 2FA d'un utilisateur cible
    W->>A: POST /users/{id}/reset-2fa
    A->>D: Delete credentials du compte cible
    D-->>A: OK
    A-->>W: success

    Note over U: Déconnexion non implémentée (fermeture navigateur)

    Note over U,W: Parcours 4 - Reconnexion avec 2FA existant
    U->>W: Saisir username
    W->>A: POST /auth/check
    A->>D: Lire credentials
    D-->>A: hasWebAuthn=true
    A-->>W: hasWebAuthn=true

    U->>W: Valider WebAuthn
    W->>A: POST /webauthn/authenticate/begin
    A-->>W: AssertionOptions
    W->>P: navigator.credentials.get(...)
    P-->>W: Assertion
    W->>A: POST /webauthn/authenticate/complete
    A->>D: Vérifier assertion + update counter
    D-->>A: OK
    A-->>W: success
    W-->>U: Accès Users.aspx

    Note over U,W: Parcours 5 - Après reset 2FA, retour au flux obligatoire
    U->>W: Nouvelle connexion du compte reset
    W->>A: POST /auth/check
    A->>D: Lire credentials
    D-->>A: hasWebAuthn=false
    A-->>W: hasWebAuthn=false
    W-->>U: Mot de passe puis enrôlement obligatoire

### 4. Endpoints minimum
1. `POST /auth/check`
2. `POST /auth/login`
3. `POST /auth/change-password`
4. `POST /webauthn/register/begin`
5. `POST /webauthn/register/complete`
6. `POST /webauthn/authenticate/begin`
7. `POST /webauthn/authenticate/complete`
8. `GET /users`
9. `POST /users`
10. `DELETE /users/{id}`
11. `POST /users/{id}/reset-2fa`

### 5. Phases et dépendances
1. Phase 0 (obligatoire): écrire le plan détaillé, s arrêter, demander validation.
2. Phase 1: Foundation API (.NET 10, DI, EF InMemory, API key middleware, CORS, config Fido2).
3. Phase 2: Auth password + policy premier login (`auth/check`, `login`, `change-password`).
4. Phase 3: WebAuthn backend (register/authenticate begin+complete, challenge store).
5. Phase 4: Intégration WebForms (login multi-étapes, change password, enroll, garde d accès).
6. Phase 5: Users UI + reset 2FA (POC sans rôles).
7. Phase 6: Fichier HTTP de tests manuels.
8. Phase 7: Validation end-to-end.

### 6. Matrice de parallélisation
1. Phase 2 dépend de Phase 1.
2. Phase 3 dépend de Phase 2.
3. Phase 4 dépend de Phases 2 et 3.
4. Phase 5 peut démarrer en parallèle partiel de Phase 4 sur la partie UI non bloquée.
5. Phase 6 peut démarrer dès que les premiers endpoints existent.
6. Phase 7 dépend de toutes les phases précédentes.
7. Maximum 2 chantiers en parallèle.
8. Interdit de paralléliser une tâche qui consomme un endpoint non implémenté.
9. Après chaque sous-lot parallèle: fusionner, vérifier, puis ouvrir un nouveau chantier.

### 7. Détail par phase (résumé exécutable)
1. Phase 1:
   1. Créer `AuthTest.Api`.
   2. Ajouter packages: Fido2NetLib, EFCore.InMemory, BCrypt.
   3. Modèles `User` + `WebAuthnCredential(Name)`.
   4. Middleware `X-Api-Key`.
   5. CORS pour origin WebForms.
2. Phase 2:
   1. Implémenter `auth/check`.
   2. Implémenter login password + token session POC.
   3. Implémenter change password.
3. Phase 3:
   1. Register begin/complete.
   2. Authenticate begin/complete.
   3. Persister credential avec Name.
4. Phase 4:
   1. Créer `AuthTest.Web`.
   2. Écran login par étapes selon `hasWebAuthn`.
   3. Écran changement mot de passe.
   4. Écran enrôlement avec nom de clé requis.
   5. Bloquer accès Users tant que non enrôlé.
5. Phase 5:
   1. Liste users.
   2. Reset 2FA par user.
   3. Suppression user.
6. Phase 6:
   1. Créer `AuthTest.http` avec scénarios minimaux.
7. Phase 7:
   1. Tests E2E des flux imposés.
   2. Corrections de régression.

## Definition of Done (orientée parcours utilisateurs)

1. Parcours Création utilisateur
Critère: un compte est créé via username + mot de passe, avec hash BCrypt stocké.
Preuve: POST /users renvoie 201.

2. Parcours Première connexion sans 2FA
Critère: auth/check renvoie hasWebAuthn=false quand aucune clé n existe.
Preuve: POST /auth/check sur compte neuf retourne hasWebAuthn=false.

3. Parcours Connexion password puis obligations
Critère: si hasWebAuthn=false, l utilisateur passe par login password, changement mot de passe obligatoire, puis enrôlement WebAuthn obligatoire.
Preuve: redirections successives vers ChangePassword puis Enroll.

4. Parcours Blocage accès applicatif avant enrôlement
Critère: tant que l enrôlement n est pas complété, accès Users refusé.
Preuve: tentative d accès Users avant enrôlement redirige vers Enroll.

5. Parcours Enrôlement WebAuthn nommé
Critère: le nom de clé est requis et persisté dans WebAuthnCredential.Name.
Preuve: register/complete sans keyName échoue, avec keyName réussit.

6. Parcours Reconnexion WebAuthn
Critère: après fermeture navigateur, auth/check renvoie hasWebAuthn=true et la connexion WebAuthn aboutit.
Preuve: authenticate begin/complete valide et accès Users accordé.

7. Parcours POC sans RBAC
Critère: tout utilisateur authentifié peut lister tous les users et reset le 2FA d un autre user.
Preuve: GET /users et POST /users/{id}/reset-2fa réussissent avec un user standard authentifié.

8. Parcours Effet reset 2FA
Critère: après reset, le compte ciblé repasse en hasWebAuthn=false et doit refaire le flux password + enrôlement obligatoire.
Preuve: auth/check du compte ciblé après reset retourne hasWebAuthn=false.

9. Parcours Testabilité API
Critère: tous les endpoints minimum sont exécutables dans le fichier HTTP de test.
Preuve: exécution manuelle complète sans endpoint manquant.

### 9. Suivi compact obligatoire
1. Un seul fichier: `AGENT_TRACKING.md`.
2. Contenu minimal:
   1. Statut global en pourcentage.
   2. Tableau phases TODO / IN_PROGRESS / DONE.
   3. Deltas récents (max 3 lignes).
   4. Prochain bloc (1 à 2 lignes).
3. Ne jamais recopier le plan complet à chaque mise à jour.