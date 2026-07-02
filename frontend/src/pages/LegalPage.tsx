import type { ReactNode } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'

/**
 * Páginas legales públicas (RGPD): /legal/terminos · /legal/privacidad · /legal/cookies.
 * Una sola página con pestañas para mantener el contenido cohesionado y fácil de mantener.
 */

const CONTACT_EMAIL = 'josejua94@gmail.com'
const CONTROLLER_NAME = 'Jose Joaquín Alarcón'
const DOMAIN = 'slotify.jjalarcon.es'
const LAST_UPDATED = 'julio de 2026'

const TABS = [
  { id: 'terminos', label: 'Términos', title: 'Términos y Condiciones de Uso' },
  { id: 'privacidad', label: 'Privacidad', title: 'Política de Privacidad' },
  { id: 'cookies', label: 'Cookies', title: 'Política de Cookies' },
] as const

type LegalSection = (typeof TABS)[number]['id']

function isLegalSection(value: string | undefined): value is LegalSection {
  return TABS.some((t) => t.id === value)
}

function Block({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="mt-stack-lg">
      <h2 className="!text-base">{title}</h2>
      <div className="mt-stack-sm space-y-stack-sm text-sm leading-relaxed text-on-surface-variant">{children}</div>
    </section>
  )
}

function TermsContent() {
  return (
    <>
      <Block title="1. Objeto del servicio">
        <p>
          Slotify ({DOMAIN}) es una plataforma de reservas en línea para negocios locales (peluquerías, clínicas,
          talleres y servicios similares). Permite a los negocios publicar sus servicios y disponibilidad, y a los
          clientes reservar citas de forma sencilla, con o sin registro previo.
        </p>
        <p>
          Slotify actúa únicamente como intermediario tecnológico: pone en contacto a clientes y negocios y gestiona
          la agenda de reservas. La prestación efectiva del servicio reservado corresponde en exclusiva al negocio.
        </p>
      </Block>

      <Block title="2. Cuentas de usuario y de negocio">
        <p>
          Los clientes pueden crear una cuenta con su email y una contraseña para gestionar sus reservas y reseñas.
          Los propietarios de negocio crean una cuenta de negocio que les permite configurar servicios, horarios,
          equipo y recibir reservas. Cada titular es responsable de la veracidad de los datos facilitados y de la
          custodia de sus credenciales.
        </p>
        <p>
          Los negocios se comprometen a mantener actualizada su disponibilidad y a atender las reservas confirmadas.
          Slotify puede suspender cuentas que hagan un uso fraudulento o abusivo de la plataforma.
        </p>
      </Block>

      <Block title="3. Reservas de invitado (sin registro)">
        <p>
          Es posible reservar sin crear cuenta indicando un teléfono o un email de contacto, que se utilizan
          exclusivamente para identificar la reserva y enviar avisos sobre la misma. Estos datos se almacenan
          cifrados (ver la <Link to="/legal/privacidad" className="font-semibold text-primary hover:underline">Política de Privacidad</Link>).
          El invitado puede consultar y cancelar sus reservas desde la sección «Mis reservas» acreditando ese mismo
          dato de contacto.
        </p>
      </Block>

      <Block title="4. Planes Free y Premium">
        <p>
          Slotify ofrece a los negocios un plan <strong>Free</strong> con límites de uso (100 reservas al mes,
          50 clientes, 5 servicios y 1 trabajador) y un plan <strong>Premium</strong> sin dichos límites. Las
          condiciones y precios del plan Premium se comunican dentro de la propia aplicación antes de contratarlo.
          Alcanzados los límites del plan Free, la plataforma impide crear nuevos elementos hasta el siguiente
          periodo o hasta la mejora de plan, sin que ello afecte a las reservas ya confirmadas.
        </p>
      </Block>

      <Block title="5. Limitación de responsabilidad">
        <p>
          La relación de prestación de servicios se establece directamente entre el cliente y el negocio. Slotify no
          es parte de esa relación y no responde de la calidad, el precio, la puntualidad ni el resultado de los
          servicios reservados, ni de las cancelaciones o incomparecencias de cualquiera de las partes.
        </p>
        <p>
          Slotify procura una disponibilidad continua de la plataforma, pero no garantiza la ausencia de
          interrupciones técnicas. En ningún caso responderá de daños indirectos derivados del uso del servicio.
        </p>
      </Block>

      <Block title="6. Baja del servicio">
        <p>
          Cualquier usuario puede darse de baja en todo momento eliminando su cuenta desde su perfil o solicitándolo
          en <a href={`mailto:${CONTACT_EMAIL}`} className="font-semibold text-primary hover:underline">{CONTACT_EMAIL}</a>.
          La baja conlleva la supresión de los datos personales conforme a la Política de Privacidad; las reservas
          futuras pendientes quedarán canceladas.
        </p>
      </Block>
    </>
  )
}

function PrivacyContent() {
  return (
    <>
      <Block title="1. Responsable del tratamiento">
        <p>
          El responsable del tratamiento de los datos personales recogidos a través de {DOMAIN} es{' '}
          <strong>{CONTROLLER_NAME}</strong>, con email de contacto{' '}
          <a href={`mailto:${CONTACT_EMAIL}`} className="font-semibold text-primary hover:underline">{CONTACT_EMAIL}</a>.
        </p>
      </Block>

      <Block title="2. Qué datos tratamos">
        <ul className="list-disc space-y-1 pl-5">
          <li>
            <strong>Usuarios registrados:</strong> email, nombre y contraseña (almacenada con hash seguro), además
            de las reservas y reseñas asociadas a la cuenta.
          </li>
          <li>
            <strong>Reservas de invitado:</strong> teléfono o email de contacto. Estos datos se almacenan{' '}
            <strong>cifrados con AES-256-GCM</strong> y nunca se incluyen en las URL de la aplicación.
          </li>
          <li>
            <strong>Negocios:</strong> nombre del negocio, servicios, horarios y datos de contacto profesionales.
          </li>
        </ul>
      </Block>

      <Block title="3. Finalidad y base legal">
        <p>
          Los datos se tratan con la única finalidad de <strong>gestionar las reservas y enviar avisos</strong>{' '}
          relacionados con ellas (confirmaciones, recordatorios y cancelaciones). La base legal es la{' '}
          <strong>ejecución del servicio</strong> solicitado (art. 6.1.b RGPD) y, para las comunicaciones de las
          reservas de invitado, el <strong>consentimiento</strong> otorgado al facilitar el dato de contacto
          (art. 6.1.a RGPD). No se elaboran perfiles ni se toman decisiones automatizadas con efectos jurídicos.
        </p>
      </Block>

      <Block title="4. Conservación">
        <p>
          Los datos de la cuenta se conservan mientras la cuenta permanezca activa. Los datos de contacto de las
          reservas de invitado se conservan mientras existan reservas asociadas y durante el plazo necesario para
          atender posibles responsabilidades. Al eliminar la cuenta, los datos personales se suprimen o anonimizan.
        </p>
      </Block>

      <Block title="5. Derechos de las personas usuarias">
        <p>
          Puedes ejercer en cualquier momento tus derechos de <strong>acceso, rectificación, supresión, oposición,
          portabilidad y limitación</strong> del tratamiento:
        </p>
        <ul className="list-disc space-y-1 pl-5">
          <li>
            Escribiendo a{' '}
            <a href={`mailto:${CONTACT_EMAIL}`} className="font-semibold text-primary hover:underline">{CONTACT_EMAIL}</a>{' '}
            indicando el derecho que deseas ejercer.
          </li>
          <li>Eliminando tu cuenta directamente desde tu perfil (derecho de supresión).</li>
        </ul>
        <p>
          Si consideras que el tratamiento no se ajusta a la normativa, puedes presentar una reclamación ante la
          Agencia Española de Protección de Datos (aepd.es).
        </p>
      </Block>

      <Block title="6. Destinatarios">
        <p>
          <strong>No se ceden datos a terceros</strong> ni se realizan transferencias internacionales. Los datos de
          una reserva solo son visibles para el negocio en el que se reserva, en la medida necesaria para prestar el
          servicio. La infraestructura de alojamiento se encuentra en la Unión Europea.
        </p>
      </Block>
    </>
  )
}

function CookiesContent() {
  return (
    <>
      <Block title="1. Uso de cookies en Slotify">
        <p>
          Slotify <strong>no utiliza cookies de terceros ni herramientas de analítica o publicidad</strong>. No hay
          rastreadores, píxeles ni cookies de seguimiento de ningún tipo.
        </p>
      </Block>

      <Block title="2. Almacenamiento local (localStorage)">
        <p>
          La aplicación únicamente utiliza el <strong>almacenamiento local del navegador (localStorage)</strong>{' '}
          para guardar el token de sesión (JWT) cuando inicias sesión. Es un almacenamiento técnico estrictamente
          necesario para mantener tu sesión abierta; no se usa para rastrearte ni se comparte con nadie, y se
          elimina al cerrar sesión.
        </p>
      </Block>

      <Block title="3. Por qué no hay banner de cookies">
        <p>
          Al no existir cookies ni tecnologías de seguimiento — solo almacenamiento técnico imprescindible para el
          funcionamiento del servicio — la normativa no exige mostrar un banner de consentimiento de cookies, y por
          eso no lo verás en Slotify.
        </p>
      </Block>
    </>
  )
}

const CONTENT: Record<LegalSection, () => ReactNode> = {
  terminos: TermsContent,
  privacidad: PrivacyContent,
  cookies: CookiesContent,
}

export function LegalPage() {
  const { seccion } = useParams()
  if (!isLegalSection(seccion)) return <Navigate to="/legal/terminos" replace />

  const tab = TABS.find((t) => t.id === seccion)!
  const Content = CONTENT[seccion]

  return (
    <div data-testid="legal-page">
      <h1>{tab.title}</h1>
      <p className="mt-1 text-xs text-on-surface-variant">Última actualización: {LAST_UPDATED}</p>

      {/* Pestañas entre documentos legales */}
      <nav className="mt-stack-md flex flex-wrap gap-stack-sm" aria-label="Documentos legales">
        {TABS.map((t) => (
          <Link
            key={t.id}
            to={`/legal/${t.id}`}
            data-testid={`legal-tab-${t.id}`}
            aria-current={t.id === seccion ? 'page' : undefined}
            className={`rounded-full px-4 py-1.5 text-sm font-semibold transition-colors ${
              t.id === seccion
                ? 'bg-primary-container text-on-primary'
                : 'bg-surface-container-high text-on-surface-variant hover:bg-surface-container-highest'
            }`}
          >
            {t.label}
          </Link>
        ))}
      </nav>

      <article className="card mt-stack-md">
        <Content />

        <p className="mt-stack-lg rounded-xl bg-surface-container-high p-stack-md text-xs leading-relaxed text-on-surface-variant">
          <span className="font-semibold">Nota:</span> este texto es una plantilla informativa elaborada para el
          proyecto académico Slotify (TFM); para un uso comercial real debe revisarse por un profesional legal.
        </p>
      </article>
    </div>
  )
}
