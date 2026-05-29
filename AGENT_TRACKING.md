# AGENT_TRACKING.md

## Statut global: 55%

| Phase | État |
|-------|------|
| Phase 0 - Plan | DONE |
| Phase 1 - Foundation API | DONE |
| Phase 2 - Auth password | DONE |
| Phase 3 - WebAuthn backend | DONE |
| Phase 4 - WebForms frontend | TODO |
| Phase 5 - Users UI | TODO |
| Phase 6 - HTTP tests | TODO |
| Phase 7 - E2E validation | TODO |

## Deltas récents
- Phase 2: AuthController (check/login/change-password), UsersController (CRUD + reset-2fa), SessionStore
- Phase 3: WebAuthnController (register+authenticate begin/complete), ChallengeStore (stocke CredentialCreateOptions/AssertionOptions)

## Prochain bloc
Phase 4: AuthTest.Web (WebForms 4.8) — login multi-étapes, changement MDP, enrôlement WebAuthn.
