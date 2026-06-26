# Despliegue en producción (VPS Ionos + GHCR + Caddy)

**Idea en una frase:** haces `git push` a `main` → GitHub compila, prueba, construye las
imágenes Docker, las sube a GHCR y se conecta por SSH al VPS para actualizarlo. No vuelves
a tocar el VPS a mano.

```
Tu PC ──push──> GitHub ──(CI: build+test+e2e)──> GHCR (imágenes)
                   │
                   └──SSH──> VPS:  Traefik(HTTPS) → frontend(nginx) → backend(API) → postgres
```

> **HTTPS:** Slotify se enchufa al **Traefik** ya existente del VPS (no levanta su propio
> proxy). Requiere que en el VPS haya un Traefik con la red externa **`traefik_net`** y un
> certresolver **`letsencrypt`** (entrypoint `websecure`). El `frontend` lleva las labels de
> Traefik para que enrute `slotify.jjalarcon.es` → Slotify. Tus otros proyectos no se tocan.

Dominio: **slotify.jjalarcon.es** · VPS user: **root**

---

## Una sola vez: preparación

### 1) DNS
Un registro **A** de `slotify.jjalarcon.es` apuntando a la **IP del VPS** (217.160.0.206).
*(Por lo que se ve en tu panel ya apunta ahí.)* Caddy necesita esto para emitir el certificado HTTPS.

### 2) En el VPS (como root)
Instala Docker + Compose y crea la carpeta de despliegue:
```bash
curl -fsSL https://get.docker.com | sh
mkdir -p /opt/slotify/infra
```

Crea el `.env` con los secretos (NO va al repo). Genera los valores:
```bash
echo "JWT_KEY=$(openssl rand -base64 48)"
echo "CRYPTO_ENCRYPTION_KEY=$(openssl rand -base64 32)"
echo "CRYPTO_BLIND_INDEX_KEY=$(openssl rand -base64 32)"
echo "POSTGRES_PASSWORD=$(openssl rand -base64 24)"
```
Y escribe `/opt/slotify/.env` (plantilla en [`.env.prod.example`](../.env.prod.example)):
```ini
DOMAIN=slotify.jjalarcon.es
POSTGRES_DB=slotify
POSTGRES_USER=slotify_user
POSTGRES_PASSWORD=...          # el generado arriba
JWT_KEY=...                    # el generado arriba
CRYPTO_ENCRYPTION_KEY=...      # 32 bytes base64
CRYPTO_BLIND_INDEX_KEY=...     # 32 bytes base64
```

### 3) Clave SSH para que GitHub entre al VPS
En tu PC:
```bash
ssh-keygen -t ed25519 -f slotify_deploy -N ""
ssh-copy-id -i slotify_deploy.pub root@217.160.0.206   # o añade slotify_deploy.pub a /root/.ssh/authorized_keys
```

### 4) GitHub → Secrets (repo → Settings → Secrets and variables → Actions)
| Secret | Valor |
|--------|-------|
| `VPS_HOST` | `217.160.0.206` |
| `VPS_USER` | `root` |
| `VPS_SSH_KEY` | el contenido de **`slotify_deploy`** (la privada, completa) |

### 5) Imágenes GHCR públicas (recomendado para el TFM)
La primera vez que despliegues se crean los *packages* `slotify-backend` y `slotify-frontend`
en GHCR (privados por defecto). Ponlos **públicos** una vez: repo → *Packages* → cada paquete →
*Package settings* → *Change visibility* → **Public**. Así el VPS los descarga sin login.
*(Alternativa si los quieres privados: añade un secret `GHCR_PAT` con permiso `read:packages` y
un `docker login ghcr.io` en el paso SSH del workflow.)*

---

## Desplegar

Automático: **fusiona a `main`** (o `git push` a `main`). La CI corre los tests y, si pasan,
el workflow [`deploy.yml`](../.github/workflows/deploy.yml) construye y publica las imágenes y
reinicia el VPS. En ~2–3 min está en `https://slotify.jjalarcon.es`.

### Primer arranque (bootstrap)
Como los packages nacen privados, en el **primer** deploy el `pull` del VPS puede fallar.
Tras esa primera ejecución: ponlos públicos (paso 5) y **re-lanza** el job *Deploy* (pestaña
Actions → Re-run). O arranca a mano una vez en el VPS:
```bash
cd /opt/slotify
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
```

---

## Comprobar / operar

```bash
# en el VPS
cd /opt/slotify
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f backend
```
- La web: `https://slotify.jjalarcon.es` · la API: `https://slotify.jjalarcon.es/api/...`
- Las **migraciones de BD corren solas** al arrancar el backend.
- Postgres **no** está expuesto a internet (solo dentro de la red Docker).

### Datos de demo (opcional)
```bash
# desde tu PC, contra producción
API=https://slotify.jjalarcon.es/api node scripts/seed-demo.mjs
```

### Backups de Postgres (recomendado en cuanto haya datos reales)
```bash
# copia puntual
docker compose -f docker-compose.prod.yml exec -T postgres \
  pg_dump -U slotify_user slotify > backup_$(date +%F).sql
# automatízalo con un cron diario en el VPS.
```

---

## Resumen
Tras la configuración inicial, **desplegar = `git push` a `main`**. Solo vuelves a tocar el VPS
si cambias un secreto (editas `/opt/slotify/.env` y reinicias: `docker compose -f docker-compose.prod.yml up -d`).
