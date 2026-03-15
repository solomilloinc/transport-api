-- Seed TenantConfig for default tenant (TenantId = 1)
-- Structured columns: identity, contact, legal
-- StyleConfigJson: theme, typography, images, landing, seo, contact.schedule

INSERT INTO [TenantConfig] (
    [TenantId],
    -- Identity
    [CompanyName],
    [CompanyNameShort],
    [CompanyNameLegal],
    [LogoUrl],
    [FaviconUrl],
    [Tagline],
    -- Contact
    [ContactAddress],
    [ContactPhone],
    [ContactEmail],
    [BookingsEmail],
    -- Legal
    [TermsText],
    [CancellationPolicy],
    -- Style JSON
    [StyleConfigJson],
    -- Audit
    [CreatedBy],
    [CreatedDate]
)
VALUES (
    1,
    -- Identity
    N'Zeros Tour',
    N'ZerosTour',
    N'Solomillo Inc',
    NULL,
    NULL,
    N'Providing safe and reliable transportation services with a family touch since 1998.',
    -- Contact
    N'Av. Alem 123, Lobos, Buenos Aires',
    N'(555) 123-4567',
    N'info@familytransit.com',
    N'bookings@familytransit.com',
    -- Legal
    N'Al completar esta reserva, usted acepta los términos y condiciones, incluida nuestra política de cancelación.',
    N'Cancelación gratuita hasta 24 horas antes de la salida. Se aplica una tarifa del 50% para cancelaciones realizadas con menos de 24 horas de antelación.',
    -- Style JSON (theme, typography, images, landing, seo, contact.schedule)
    N'{"theme":{"light":{"primary":"221.2 83.2% 53.3%","primaryForeground":"210 40% 98%","background":"0 0% 100%","foreground":"222.2 84% 4.9%","card":"0 0% 100%","cardForeground":"222.2 84% 4.9%","secondary":"210 40% 96.1%","secondaryForeground":"222.2 47.4% 11.2%","muted":"210 40% 96.1%","mutedForeground":"215.4 16.3% 46.9%","accent":"210 40% 96.1%","accentForeground":"222.2 47.4% 11.2%","destructive":"0 84.2% 60.2%","destructiveForeground":"210 40% 98%","border":"214.3 31.8% 91.4%","input":"214.3 31.8% 91.4%","ring":"221.2 83.2% 53.3%","radius":"0.5rem"},"dark":{"primary":"217.2 91.2% 59.8%","primaryForeground":"222.2 47.4% 11.2%","background":"222.2 84% 4.9%","foreground":"210 40% 98%","card":"222.2 84% 4.9%","cardForeground":"210 40% 98%","secondary":"217.2 32.6% 17.5%","secondaryForeground":"210 40% 98%","muted":"217.2 32.6% 17.5%","mutedForeground":"215 20.2% 65.1%","accent":"217.2 32.6% 17.5%","accentForeground":"210 40% 98%","destructive":"0 62.8% 30.6%","destructiveForeground":"210 40% 98%","border":"217.2 32.6% 17.5%","input":"217.2 32.6% 17.5%","ring":"212.7 26.8% 83.9%","radius":"0.5rem"},"brandColors":{"50":"#eef3fb","100":"#d4e1f5","200":"#b0c9ed","300":"#84a9e2","400":"#5785d5","500":"#3366cc","600":"#2952a3","700":"#1f3d7a","800":"#142952","900":"#0a1429","950":"#050a14"},"accentColor":"#facc15"},"typography":{"bodyFont":"Geist","displayFont":"Montserrat"},"images":{"heroBackground":"/background.jpg","aboutPhoto":null,"routesMap":null,"openGraphImage":null},"landing":{"hero":{"title":"Viajes Seguros con un Toque Familiar","subtitle":"Brindando transporte de corta distancia confiable y cómodo por más de 25 años. Donde cada pasajero es tratado como familia.","ctaPrimary":"Reservá tu Viaje","ctaSecondary":"Conocé Más"},"about":{"title":"Sobre Nuestra Empresa Familiar","paragraphs":["Fundada en 1998, Zeros Tour ha brindado servicios de transporte seguros, confiables y cómodos a nuestra comunidad durante más de 25 años.","Como empresa familiar, nos enorgullece tratar a cada pasajero como un miembro de nuestra propia familia."],"features":[{"icon":"Shield","title":"Seguridad Primero","description":"Mantenimiento riguroso y conductores capacitados"},{"icon":"Clock","title":"Puntualidad","description":"Salidas y llegadas a tiempo"},{"icon":"Users","title":"Valores Familiares","description":"Atención personalizada a cada pasajero"},{"icon":"Bus","title":"Flota Moderna","description":"Vehículos cómodos y bien mantenidos"}]},"routesTitle":"Nuestras Rutas Populares","routesSubtitle":"Conectamos comunidades con un servicio frecuente y confiable en estas rutas populares.","routes":[{"label":"Lobos ↔ Buenos Aires","duration":"90 min"},{"label":"La Plata ↔ Luján","duration":"120 min"},{"label":"Cañuelas ↔ Navarro","duration":"45 min"},{"label":"Mercedes ↔ Roque Pérez","duration":"60 min"}],"testimonialsTitle":"Lo que Dicen Nuestros Pasajeros","testimonialsSubtitle":"No te fíes solo de nuestra palabra.","testimonials":[{"name":"Sara Johnson","comment":"Siempre puntuales y los conductores son muy amables.","rating":5},{"name":"Miguel Rodríguez","comment":"Valoro mucho el cuidado y la atención extra.","rating":5},{"name":"Emilia Chen","comment":"El sistema de reservas online es súper práctico.","rating":4}],"cta":{"title":"¿Listo para Vivir la Experiencia?","subtitle":"Reservá tu próximo viaje con nosotros.","ctaPrimary":"Reservá tu Viaje Ahora","ctaSecondary":"Ver Horarios"}},"contact":{"schedule":["Lunes a Viernes: 5:00 AM - 10:00 PM","Sábados y Domingos: 6:00 AM - 9:00 PM"]},"seo":{"title":"Reserva de Pasajes","description":"Aplicación para la reserva de pasajes de transporte.","keywords":["transporte","reservas","pasajes","viajes","autobús","tour"]}}',
    -- Audit
    'System',
    GETDATE()
);

UPDATE [TenantConfig]
SET
    -- Identity
    CompanyName = N'Zeros Tour'
WHERE TenantConfigId = 1;

-- Update Tenant name and code to match
UPDATE [Tenant]
SET [Name] = N'Zeros Tour',
    [Code] = 'zerostour',
	[Status] = 1,
	[Domain]='http://localhost:3000/'
WHERE [TenantId] = 1;

 INSERT INTO [TenantPaymentConfig] (
      [TenantId],
      [AccessToken],
      [PublicKey],
      [WebhookSecret],
      [Status],
      [CreatedBy],
      [CreatedDate]
  )
  VALUES (
      1,
      'TEST-1300259465897218-011222-98183b7f31ece34d1cca1f1d792fcc33-3129321275',
      'TEST-dffe6fe7-40bc-4bb5-b917-b3f34b858140',
      '89e193a718ab9646cc4863df17335502d70f24c83cafe824d685a74ac1b46f99',
      0,
      'System',
      GETDATE()
  );