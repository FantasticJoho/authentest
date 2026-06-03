## Backlog découpé en Epics / User Stories

### Epic 1 - Fondations 2FA WebAuthn (Backend)
**Objectif:** préparer le socle technique WebAuthn et la persistance des clés nommées.

#### US 1.1 - Modèle de credential WebAuthn
En tant que développeur, je veux stocker les credentials WebAuthn nommés par utilisateur afin de supporter le 2FA et le multi-clés.

**Critères d acceptation**
1. Une entité `WebAuthnCredential` existe, liée à `User` en one-to-many.
2. Les champs minimum sont présents: `CredentialId`, `PublicKey`, `SignatureCounter`, `UserHandle`, `Name`, `CreatedAt`.
3. Le champ `Name` est requis.
4. Un index unique sur `CredentialId` est en place.

#### US 1.2 - Détection état 2FA à la connexion
En tant que frontend, je veux connaître l état `hasWebAuthn` pour orienter le parcours utilisateur.

**Critères d acceptation**
1. `auth/check` renvoie `hasWebAuthn=false` si aucune clé.
2. `auth/check` renvoie `hasWebAuthn=true` si au moins une clé.
3. Après reset 2FA, `auth/check` renvoie `hasWebAuthn=false`.

---

### Epic 2 - Enrôlement obligatoire si 2FA absente
**Objectif:** imposer l enrôlement WebAuthn après login password quand aucune clé n existe.

#### US 2.1 - Begin enrollment WebAuthn
En tant qu utilisateur sans 2FA, je veux démarrer l enrôlement WebAuthn pour enregistrer mon device.

**Critères d acceptation**
1. Endpoint `webauthn/register/begin` disponible.
2. Un challenge est généré côté serveur.
3. Le challenge est stocké temporairement.

#### US 2.2 - Complete enrollment avec nom de clé
En tant qu utilisateur, je veux nommer ma clé lors de l enrôlement afin de l identifier plus tard.

**Critères d acceptation**
1. Endpoint `webauthn/register/complete` valide l attestation.
2. `keyName` est obligatoire et non vide.
3. La clé est persistée avec `Name=keyName`.
4. En l absence de `keyName`, la requête échoue.

#### US 2.3 - Garde d accès avant enrôlement
En tant que système, je veux bloquer l accès applicatif tant que l enrôlement n est pas terminé.

**Critères d acceptation**
1. Si `hasWebAuthn=false`, après login password l utilisateur est redirigé vers enrollment.
2. L accès à la liste users est refusé avant fin enrôlement.
3. Après enrôlement réussi, accès applicatif autorisé.

---

### Epic 3 - Connexion avec 2FA existante
**Objectif:** permettre l authentification WebAuthn quand une clé existe déjà.

#### US 3.1 - Begin authentication WebAuthn
En tant qu utilisateur avec 2FA active, je veux initier une connexion WebAuthn.

**Critères d acceptation**
1. Endpoint `webauthn/authenticate/begin` disponible.
2. Un challenge d authentification est généré selon les credentials du user.
3. Les options sont retournées au front.

#### US 3.2 - Complete authentication WebAuthn
En tant qu utilisateur, je veux finaliser ma connexion WebAuthn.

**Critères d acceptation**
1. Endpoint `webauthn/authenticate/complete` valide l assertion.
2. `SignatureCounter` est mis à jour.
3. Une session applicative est ouverte en cas de succès.
4. En cas d assertion invalide, accès refusé.

---

### Epic 4 - Reset 2FA et comportement post-reset
**Objectif:** permettre la remise à zéro des clés et garantir le retour au flux d enrôlement.

#### US 4.1 - Reset 2FA d un utilisateur
En tant qu utilisateur authentifié (POC), je veux supprimer les clés WebAuthn d un compte cible.

**Critères d acceptation**
1. Endpoint `users/{id}/reset-2fa` supprime toutes les clés WebAuthn du compte cible.
2. L action est accessible à tout utilisateur authentifié (POC sans RBAC).
3. Une confirmation de succès est retournée.

#### US 4.2 - Flux de connexion après reset
En tant que compte reset, je veux repasser par le flux password + enrôlement obligatoire.

**Critères d acceptation**
1. Après reset, `auth/check` renvoie `hasWebAuthn=false`.
2. La connexion suivante ne propose plus WebAuthn.
3. Le flux impose login password puis enrôlement obligatoire.

---

### Epic 5 - Frontend WebForms (parcours utilisateur)
**Objectif:** implémenter les écrans et redirections cohérents avec les règles métier.

#### US 5.1 - Login orienté par `hasWebAuthn`
En tant qu utilisateur, je veux un écran de login qui me propose le bon mode de connexion selon mon état 2FA.

**Critères d acceptation**
1. Si `hasWebAuthn=false`, affichage flow password.
2. Si `hasWebAuthn=true`, proposition flow WebAuthn.
3. Les redirections respectent le parcours défini.

#### US 5.2 - Écran enrôlement avec clé nommée
En tant qu utilisateur, je veux saisir un nom de clé et enrôler mon device.

**Critères d acceptation**
1. Champ nom de clé obligatoire côté UI.
2. Enrôlement WebAuthn via browser API.
3. Appel `register/complete` inclut `keyName`.
4. Succès => accès à la liste users.

#### US 5.3 - Liste users + action reset 2FA
En tant qu utilisateur authentifié (POC), je veux lister les users et reset leur 2FA.

**Critères d acceptation**
1. La liste users s affiche.
2. L action reset 2FA est disponible par ligne.
3. Après reset, le comportement du compte cible est conforme (retour flow enrôlement).

---

### Epic 6 - Sécurité technique et intégration
**Objectif:** assurer le fonctionnement inter-apps en local.

#### US 6.1 - Sécurisation des appels Front -> API
En tant que système, je veux protéger les appels API avec une clé d API fixe.

**Critères d acceptation**
1. Header `X-Api-Key` requis côté API.
2. Le front envoie systématiquement `X-Api-Key`.
3. Appel sans clé valide refusé.

#### US 6.2 - CORS pour apps séparées
En tant que système, je veux autoriser le front local à appeler l API.

**Critères d acceptation**
1. Origines front locales autorisées.
2. Requêtes cross-origin fonctionnelles.
3. Origines non autorisées rejetées.

---

### Epic 7 - Tests et validation E2E
**Objectif:** prouver que les parcours utilisateurs demandés sont couverts.

#### US 7.1 - Fichier HTTP de tests manuels
En tant que développeur, je veux un fichier HTTP pour rejouer les endpoints principaux.

**Critères d acceptation**
1. Le fichier couvre create/check/login/change-password/register/authenticate/users/reset.
2. Les scénarios nominaux sont exécutables sans modification majeure.
3. Les réponses attendues sont documentées.

#### US 7.2 - Validation des parcours utilisateurs
En tant que PO, je veux vérifier les parcours bout en bout.

**Critères d acceptation**
1. Parcours user sans 2FA validé (password -> change password -> enrollment).
2. Parcours user avec 2FA validé (connexion WebAuthn).
3. Parcours reset 2FA validé (retour `hasWebAuthn=false`).
4. Accès applicatif bloqué avant enrôlement validé.

---

### Epic 8 - Observabilite et exploitation
**Objectif:** rendre le module WebAuthn exploitable en production (monitoring + disponibilite).

#### US 8.1 - Metriques et alerting WebAuthn
En tant qu equipe exploitation, je veux suivre les metriques critiques WebAuthn et recevoir des alertes en cas d anomalie.

**Critères d acceptation**
1. Les metriques minimum sont exposees: latence, taux d erreur, taux de succes, volume par endpoint.
2. Les endpoints couverts incluent `webauthn/register/begin`, `webauthn/register/complete`, `webauthn/authenticate/begin`, `webauthn/authenticate/complete`.
3. Des alertes sont configurees sur seuils (ex: hausse erreurs, baisse succes, latence elevee).
4. Le runbook d investigation de base est documente.

#### US 8.2 - Health checks, readiness, liveness
En tant qu equipe plateforme, je veux des checks de sante pour piloter correctement le deploiement et la supervision.

**Critères d acceptation**
1. Un endpoint `health/live` indique si le process est vivant.
2. Un endpoint `health/ready` indique si les dependances critiques sont disponibles (DB, cache/store challenge).
3. Le deploiement supporte un mode sans coupure (rolling, blue/green, ou canary selon infra).
4. Les checks sont integres aux probes de la plateforme d hebergement.


## Tableau de synthèse - Complexité par Epic (profil senior 8-10 ans)

| Epic | Complexité | Estimation (jours ouvrés) | Risque principal | Dépendances clés |
|---|---|---:|---|---|
| Epic 1 - Fondations 2FA WebAuthn (Backend) | Medium | 1.5 à 2.5 | Mauvais mapping modèle <-> objets WebAuthn | Aucune (point de départ) |
| Epic 2 - Enrôlement obligatoire si 2FA absente | High | 2.5 à 4 | Orchestration du flow obligatoire + challenge state | Epic 1 |
| Epic 3 - Connexion avec 2FA existante | High | 2 à 3.5 | Validation assertion + comportements navigateur/device | Epic 1, Epic 2 (partiel) |
| Epic 4 - Reset 2FA et comportement post-reset | Medium | 1 à 1.5 | Incohérences d état hasWebAuthn après reset | Epic 1, Epic 2 |
| Epic 5 - Frontend WebForms (parcours utilisateur) | High | 3 à 5 | Gestion d état multi-étapes dans WebForms | Epic 2, Epic 3, Epic 4 |
| Epic 6 - Sécurité technique et intégration | Medium | 1 à 2 | CORS / API key / configs locales | Epic 1 (et en parallèle partiel des autres) |
| Epic 7 - Tests et validation E2E | Medium/High | 2 à 3.5 | Couverture réelle des parcours + stabilité des tests | Tous les epics précédents |

### Totaux projet

| Scénario | Charge totale |
|---|---:|
| Optimiste | 13.5 j |
| Réaliste | 18 à 22 j |
| Prudent (aléas WebAuthn) | ~25 j |

### Priorisation recommandée (ordre d exécution)

1. Epic 1
2. Epic 2
3. Epic 3
4. Epic 4
5. Epic 5
6. Epic 6 (peut démarrer en parallèle partiel)
7. Epic 7