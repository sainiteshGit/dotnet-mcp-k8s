# Pull Request

## Summary

<!-- One-paragraph description of the change and the user-visible impact. -->

## Linked spec / task IDs

- Spec: `specs/001-task-manager-api/`
- Tasks: e.g. `T0xx`, `T0yy`

## Type of change

- [ ] Feature (user-visible behaviour change)
- [ ] Bugfix
- [ ] Infrastructure / CI
- [ ] Tests only
- [ ] Docs only
- [ ] Refactor (no behaviour change)

## Test evidence

- [ ] New / changed code is covered by failing-first tests (Principle III)
- [ ] `dotnet test` runs locally and is green
- [ ] Aggregate line coverage stays ≥ 80 % (Principle III gate)

## Security checklist

- [ ] No new secrets, tokens, passwords, or connection strings added in any layer (Principle V)
- [ ] No new public ingress, no new privileged containers, no host mounts (Principle IV)
- [ ] Workload Identity / GitHub OIDC remains the only auth path to Azure
- [ ] Trivy scan still green on changed images

---

## Constitution check (mandatory — required by `.specify/memory/constitution.md`)

> **I have re-read the binding constitution and confirm this PR complies with every principle marked NON-NEGOTIABLE (I, III, V), respects the documented Principle II dev-overlay exception (mutations remain disabled in prod), and introduces no new exception requiring a complexity-tracking entry.**

- [ ] Confirmed.
