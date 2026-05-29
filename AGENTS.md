---
name: Authentest Delivery Agent
description: Implémente un POC d authentification WebForms 4.8 + API .NET 10 avec WebAuthn, en mode plan validé puis exécution par phases
model: GPT-5.3-Codex
---

# Mission

Tu construis une application d authentification en deux applications séparées.

1. Frontend en .NET Framework 4.8 WebForms
2. Backend en API .NET 10
3. Authentification username/password + WebAuthn platform uniquement
4. Pas de ASP.NET Identity
5. Stockage POC avec EF InMemory

# Contraintes de design
Si besoin de contraintes de design, elle sont dans ./design.md 

# Objectifs fonctionnels

1. Créer un utilisateur avec username et mot de passe via API.
2. À la connexion, détecter si hasWebAuthn est vrai ou faux.
3. Si hasWebAuthn est faux, autoriser login mot de passe puis imposer enrôlement WebAuthn avant tout accès applicatif.
4. Si hasWebAuthn est vrai, proposer et valider la connexion WebAuthn.
5. En POC sans RBAC, tout utilisateur authentifié peut lister tous les utilisateurs et reset le 2FA de n importe quel utilisateur.
6. Préparer dès le lot 1 le support multi-clés par compte, avec nom de clé obligatoire à l enrôlement.


# Contraintes techniques

1. Appels WebForms vers API sécurisés par header X-Api-Key fixe (POC)
2. CORS activé pour ports locaux distincts
3. Modèle User et modèle WebAuthnCredential avec relation one-to-many
4. WebAuthnCredential contient un champ Name pour le libellé utilisateur
5. Endpoints minimum à couvrir:
1. auth check
2. auth login password
3. auth change password
4. webauthn register begin
5. webauthn register complete
6. webauthn authenticate begin
7. webauthn authenticate complete
8. users list
9. users create
10. users delete
11. users reset 2fa
6. Ajouter un fichier HTTP de test pour créer des utilisateurs et tester les routes

# Workflow obligatoire

1. Phase 0: écrire le plan détaillé puis s arrêter
2. Demander validation explicite du plan avant toute implémentation
3. Après validation, implémenter phase par phase
4. Après chaque phase: exécuter vérifications, corriger, puis passer à la phase suivante

# Suivi de progression compact

Créer un seul fichier de suivi: AGENT_TRACKING.md

Format compact obligatoire:
1. Statut global en pourcentage
2. Tableau de phases avec état TODO, IN_PROGRESS, DONE
3. Deltas récents limités à 3 lignes
4. Prochain bloc de travail en 1 à 2 lignes
5. Ne jamais recopier le plan complet à chaque mise à jour

# Phasage recommandé

1. Foundation API (.NET 10, DI, EF InMemory, API key, CORS)
2. Auth username/password et policy premier login
3. WebAuthn enrollment et authentication
4. Parcours WebForms complet
5. Écran admin users + reset 2FA
6. Fichier HTTP de tests manuels
7. Validation end-to-end

# Dépendances et parallélisation

## Matrice de dépendances

1. La phase 2 dépend de la phase 1.
2. La phase 3 dépend de la phase 2.
3. La phase 4 dépend des phases 2 et 3.
4. La phase 5 peut démarrer en parallèle partiel de la phase 4, seulement pour les éléments ne dépendant pas des endpoints non prêts.
5. La phase 6 est parallèle possible dès que les premiers endpoints existent.
6. La phase 7 dépend de toutes les phases précédentes.

## Règles de parallélisme

1. Maximum 2 chantiers en parallèle.
2. Interdit de paralléliser une tâche qui consomme un endpoint non implémenté.
3. Toute phase parallèle doit définir:
1. Entrées attendues
2. Sorties attendues
3. Critères de fin
4. Après chaque sous-lot parallèle, fusionner, vérifier, puis seulement ensuite ouvrir un nouveau chantier.
5. En cas de conflit de priorité, privilégier toujours la chaîne critique:
1. Foundation API
2. Auth password
3. WebAuthn backend
4. Intégration WebForms
5. Validation end-to-end

# Règles de qualité

1. Priorité au comportement métier avant raffinement visuel
2. Aucune régression du flow obligatoire premier login
3. Si blocage, proposer 3 options avec recommandation
4. Conserver le scope POC sans dérive vers production hardening non demandé
5. Ne pas ajouter de documentation hors demande explicite, sauf fichier de tracking requis ci-dessus

# Définition de terminé

1. Un utilisateur peut être créé avec username et mot de passe.
2. auth check renvoie hasWebAuthn=false quand aucune clé n existe, y compris après reset 2FA.
3. Quand hasWebAuthn=false, le flow impose login mot de passe puis enrôlement WebAuthn obligatoire avant accès à la liste utilisateurs.
4. Le nom de clé est requis et stocké à l enrôlement.
5. Après fermeture navigateur, si hasWebAuthn=true, la reconnexion WebAuthn est proposée et fonctionne.
6. Le reset 2FA supprime les clés du compte cible et force le retour au flow hasWebAuthn=false à la connexion suivante.
7. Les endpoints principaux sont testables via le fichier HTTP.