// Datos de prueba para el TFM. Crea, vía la API pública, un negocio demo con servicios,
// horario, reservas (pasadas y futuras) y una reseña, además de un cliente demo.
//
//   Requisitos: la API en marcha (docker compose up -d) y Node 22+ (fetch global).
//   Uso:        node scripts/seed-demo.mjs            (API por defecto en :5000)
//               API=http://localhost:5000 node scripts/seed-demo.mjs
//
// Pensado para una BD limpia. Es idempotente en las cuentas (si ya existen, hace login),
// pero al re-ejecutarlo añadiría reservas nuevas.

const API = process.env.API || 'http://localhost:5000'
const PASSWORD = 'Demo1234!'

const OWNER = { email: 'owner@demo.slotify', name: 'Lucía (dueña)', businessName: 'Barbería Demo' }
const CUSTOMER = { email: 'cliente@demo.slotify', name: 'Carlos Cliente' }

async function api(path, { method = 'GET', token, body } = {}) {
  const res = await fetch(`${API}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  })
  const text = await res.text()
  const data = text ? JSON.parse(text) : null
  if (!res.ok) throw new Error(`${method} ${path} → ${res.status}: ${text}`)
  return data
}

/** Registra (o si ya existe, hace login) y devuelve el AuthResult. */
async function registerOrLogin(register, login) {
  try {
    return await register()
  } catch (e) {
    if (String(e).includes('409') || String(e.message).includes('409')) return await login()
    throw e
  }
}

function isoLocal(daysFromNow, hour, minute = 0) {
  const d = new Date()
  d.setDate(d.getDate() + daysFromNow)
  d.setHours(hour, minute, 0, 0)
  return d.toISOString()
}

async function main() {
  console.log(`▶ Sembrando datos demo en ${API} …\n`)

  // 1) Owner + negocio (Free)
  const owner = await registerOrLogin(
    () => api('/auth/register-owner', { method: 'POST', body: { ...OWNER, password: PASSWORD } }),
    () => api('/auth/login', { method: 'POST', body: { email: OWNER.email, password: PASSWORD } }),
  )
  const businessId = owner.businessId
  const ownerToken = owner.accessToken
  console.log(`✔ Owner: ${OWNER.email} / ${PASSWORD}  ·  negocio "${OWNER.businessName}" (${businessId})`)

  // 2) Servicios
  const services = []
  for (const s of [
    { name: 'Corte de pelo', durationMinutes: 30, price: 15 },
    { name: 'Corte + barba', durationMinutes: 45, price: 22 },
    { name: 'Afeitado clásico', durationMinutes: 20, price: 12 },
  ]) {
    services.push(await api(`/businesses/${businessId}/services`, { method: 'POST', token: ownerToken, body: s }))
  }
  console.log(`✔ ${services.length} servicios`)

  // 3) Horario L–V 09:00–18:00
  await api(`/businesses/${businessId}/hours`, {
    method: 'PUT', token: ownerToken,
    body: { days: [1, 2, 3, 4, 5].map((d) => ({ dayOfWeek: d, isClosed: false, openingTime: '09:00:00', closingTime: '18:00:00' })) },
  })
  console.log('✔ Horario L–V 09:00–18:00')

  // 4) Perfil público (categoría) para que salga bonito en Explorar
  await api(`/businesses/${businessId}/profile`, {
    method: 'PUT', token: ownerToken,
    body: { category: 'barberia', photoUrl: null, latitude: null, longitude: null },
  })

  const staff = await api(`/businesses/${businessId}/staff`)
  const staffId = staff[0].id

  // 5) Cliente registrado
  const customer = await registerOrLogin(
    () => api('/auth/register', { method: 'POST', body: { ...CUSTOMER, password: PASSWORD } }),
    () => api('/auth/login', { method: 'POST', body: { email: CUSTOMER.email, password: PASSWORD } }),
  )
  console.log(`✔ Cliente: ${CUSTOMER.email} / ${PASSWORD}`)

  // 6) Reservas: una pasada del cliente (para reseñar) + dos futuras (cliente + invitado)
  const past = await api('/reservations', {
    method: 'POST', token: customer.accessToken,
    body: { businessId, serviceId: services[0].id, staffId, startTime: isoLocal(-3, 11) },
  })
  await api('/reservations', {
    method: 'POST', token: customer.accessToken,
    body: { businessId, serviceId: services[1].id, staffId, startTime: isoLocal(2, 10) },
  })
  await api('/reservations', {
    method: 'POST', token: ownerToken,
    body: { businessId, serviceId: services[2].id, staffId, startTime: isoLocal(1, 12, 30), guestName: 'Marta Invitada', guestPhone: '+34611223344' },
  })
  console.log('✔ 3 reservas (1 pasada + 2 futuras)')

  // 7) Reseña del cliente sobre el negocio (desde la reserva pasada)
  await api(`/reservations/${past.id}/review`, {
    method: 'POST', token: customer.accessToken,
    body: { rating: 5, comment: '¡Genial! Muy buen trato y puntualidad.' },
  })
  console.log('✔ 1 reseña (5★)')

  console.log('\n✅ Datos demo listos.\n')
  console.log('   Owner   →', OWNER.email, '/', PASSWORD)
  console.log('   Cliente →', CUSTOMER.email, '/', PASSWORD)
}

main().catch((e) => { console.error('\n✖ Error sembrando datos:', e.message); process.exit(1) })
