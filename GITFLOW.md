# Git Workflow - Slotify

## Branches

```
main (releases, production)
  ↑ (merge cuando hay release)
  |
develop (staging, development hub)
  ↑ (merge cuando feature está listo)
  |
feature/* (features individuales)
  feature/auth-register
  feature/guest-booking
  feature/doble-booking-prevention
  ...
```

## Default Branch

- **GitHub default:** `develop`
- **Production:** `main`

---

## Workflow (TDD)

### 1. Crear Feature Branch

```bash
git checkout develop
git pull origin develop

# Crear feature branch desde develop
git checkout -b feature/auth-register
```

**Naming:** `feature/{domain}/{description}`
- `feature/auth-register`
- `feature/reservations-availability`
- `feature/notifications-email`

### 2. Desarrollo con TDD

```bash
# 1. Escribir test
# (test falla - RED)

# 2. Escribir código
# (test pasa - GREEN)

# 3. Refactor
# (tests siguen pasando)

# 4. Commit atómico
git add .
git commit -m "feat(auth): implement user registration with tests"
```

### 3. Push a GitHub

```bash
git push -u origin feature/auth-register
```

### 4. Abrir Pull Request en GitHub

- Title: `feat(auth): implement user registration`
- Description: Qué cambia, por qué, tests incluidos
- Assign to yourself
- Link issue si existe

### 5. Self-Review & Merge

```bash
# En GitHub: Review cambios
# Si OK → "Squash and merge" a develop

# O local:
git checkout develop
git pull origin develop
git merge feature/auth-register
git push origin develop

# Eliminar feature branch
git branch -d feature/auth-register
git push origin --delete feature/auth-register
```

---

## Commits (Conventional Commits)

Formato: `<type>(<scope>): <subject>`

### Types

- `feat`: Nueva feature
- `fix`: Bugfix
- `test`: Tests (sin cambios de código)
- `docs`: Documentación
- `refactor`: Refactor sin cambios de comportamiento
- `perf`: Mejora de performance
- `infra`: Docker, CI/CD, deployment

### Scope (Opcional)

- `auth`: Autenticación
- `reservations`: Reservas
- `notifications`: Notificaciones
- `database`: BD/Migrations
- etc.

### Ejemplos

```
feat(auth): add user registration endpoint
test(reservations): add double-booking prevention tests
fix(notifications): handle email delivery timeout
docs(api): update reservation endpoint documentation
refactor(auth): extract token generation to service
perf(availability): optimize slot availability query
infra(docker): update postgresql version to 17
```

---

## Release Flow (main)

Cuando vayas a release a producción:

```bash
# En develop, asegúrate que todo esté ready
git checkout develop
git pull origin develop

# Crear release branch
git checkout -b release/v1.0.0

# Bumear versión en appsettings.json, package.json, etc.
# Actualizar CHANGELOG.md

git commit -m "chore(release): v1.0.0"
git push -u origin release/v1.0.0

# En GitHub: Abrir PR de release/v1.0.0 → main
# Merge → crea release tag v1.0.0

# Luego: Merge main → develop
git checkout develop
git pull origin main
git push origin develop
```

---

## Hotfix (Si necesario)

```bash
# Crear desde main
git checkout main
git pull origin main
git checkout -b hotfix/critical-bug

# Fix + test
# Commit

git push -u origin hotfix/critical-bug

# En GitHub: PR hotfix → main
# Merge → tag v1.0.1

# Luego: Merge main → develop
git checkout develop
git pull origin main
git push origin develop
```

---

## Reglas

1. **Nunca pushear directo a develop o main**
   - Siempre via PR con self-review

2. **Tests deben pasar antes de merge**
   ```bash
   dotnet test  # backend
   npm run test  # frontend
   ```

3. **Cada commit debe dejar proyecto en estado funcional**
   - Compilable
   - Tests pasan

4. **Feature branches: limp(ias) y atómicas**
   - Una feature = una rama
   - Max 1-2 semanas de desarrollo
   - Si es muy grande → split en sub-features

5. **Squash commits al mergear** (opcional)
   ```bash
   git merge --squash feature/xxx
   git commit -m "feat(xxx): ..."
   ```

---

## Comandos Útiles

```bash
# Ver ramas locales
git branch

# Ver ramas remotas
git branch -r

# Ver historial (bonito)
git log --oneline --graph --all

# Ver cambios entre branches
git diff develop..feature/auth

# Limpiar branches locales eliminadas en remote
git fetch -p

# Ver estado
git status

# Ver commits no pusheados
git log origin/develop..develop

# Rebase en develop (antes de pushear)
git fetch origin
git rebase origin/develop

# Abortar cambios locales
git reset --hard origin/develop
```

---

## Flujo Visual (Fase 3)

```
[develop] ← unstable, en desarrollo
    ↑
    ├─ feature/auth-register ← TDD in progress
    │   └─ test: user registration
    │   └─ feat: endpoint + service
    │   └─ PR → develop (squash merge)
    │
    ├─ feature/reservations-guest ← siguiente
    └─ feature/notifications-email ← después
```

---

## GitHub PR Checklist

Antes de mergear:

- [ ] Tests pasar (local + GitHub Actions)
- [ ] Código buildea sin warnings
- [ ] Documentación actualizada (si aplica)
- [ ] Commit messages son descriptivos
- [ ] Sin secrets o archivos sensibles
- [ ] Cambios son coherentes (una feature = una PR)

---

## Ejemplo Completo: Auth Register

```bash
# 1. Setup
git checkout develop && git pull origin develop

# 2. Feature branch
git checkout -b feature/auth-register

# 3. Tests (RED)
# → escribir tests en Slotify.Tests/

# 4. Code (GREEN)
# → implementar en Slotify.API/, Slotify.Domain/

# 5. Refactor
# → mejorar, limpiar, documentar

# 6. Commit
git add .
git commit -m "feat(auth): implement user registration with bcrypt hashing"

# 7. Tests locales
dotnet test

# 8. Push
git push -u origin feature/auth-register

# 9. GitHub
# → Open PR feature/auth-register → develop
# → Review + approve
# → Squash and merge

# 10. Limpiar
git checkout develop
git pull origin develop
git branch -d feature/auth-register
git push origin --delete feature/auth-register
```

Done. Ahora `develop` tiene la feature y está listo para siguiente.
