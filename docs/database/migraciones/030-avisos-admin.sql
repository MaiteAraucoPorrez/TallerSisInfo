SET NAMES utf8mb4;

-- Avisos para el administrador (campanita del admin). Lleva una base con la 029 a la versión actual de schema.sql.
-- Se ejecuta UNA sola vez, con la base de datos ya seleccionada (sin USE). No hace falta en una base nueva creada con el
-- schema.sql actual. Volver a ejecutarla no rompe nada (reemplaza los mismos CHECK).
--
-- Qué cambia (solo los CHECK de notificacion; ninguna columna ni dato):
--   · REPORTE_NUEVO (con reporte_id): un estudiante envió un reporte. Le llega a cada admin activo.
--   · EVENTO_POR_REVISAR (con evento_id): un evento quedó pendiente de revisión. Le llega a cada admin activo.
--   Cuando un admin resuelve el reporte o modera el evento, el aviso queda leído para todos los admins.
--   uq_notif_reporte y uq_notif_evento (usuario, objeto, tipo) ya evitan avisos repetidos.
-- Si la base no la tiene, el backend sigue funcionando: los avisos al admin simplemente no se guardan.

ALTER TABLE notificacion
  DROP CHECK ck_notif_tipo,
  DROP CHECK ck_notif_ref,
  ADD CONSTRAINT ck_notif_tipo CHECK (tipo IN ('PUENTE_DISPONIBLE','DESPUES_DE_CLASES','INVITACION_COMUNIDAD','COMUNIDAD_CERRADA',
                                           'EVENTO_APROBADO','EVENTO_RECHAZADO','EVENTO_BAJADO','REPORTE_RESUELTO','SANCION_ADVERTENCIA',
                                           'CONEXION_SOLICITADA','CONEXION_ACEPTADA','REPORTE_NUEVO','EVENTO_POR_REVISAR')),
  ADD CONSTRAINT ck_notif_ref CHECK (
       (tipo IN ('PUENTE_DISPONIBLE','DESPUES_DE_CLASES')
          AND puente_fecha IS NOT NULL AND puente_hora_inicio IS NOT NULL AND puente_hora_fin IS NOT NULL
          AND comunidad_id IS NULL AND evento_id IS NULL AND reporte_id IS NULL AND sancion_id IS NULL AND conexion_id IS NULL)
    OR (tipo IN ('INVITACION_COMUNIDAD','COMUNIDAD_CERRADA') AND comunidad_id IS NOT NULL
          AND puente_fecha IS NULL AND puente_hora_inicio IS NULL AND puente_hora_fin IS NULL
          AND evento_id IS NULL AND reporte_id IS NULL AND sancion_id IS NULL AND conexion_id IS NULL)
    OR (tipo IN ('EVENTO_APROBADO','EVENTO_RECHAZADO','EVENTO_BAJADO','EVENTO_POR_REVISAR') AND evento_id IS NOT NULL
          AND puente_fecha IS NULL AND puente_hora_inicio IS NULL AND puente_hora_fin IS NULL
          AND comunidad_id IS NULL AND reporte_id IS NULL AND sancion_id IS NULL AND conexion_id IS NULL)
    OR (tipo IN ('REPORTE_RESUELTO','REPORTE_NUEVO') AND reporte_id IS NOT NULL
          AND puente_fecha IS NULL AND puente_hora_inicio IS NULL AND puente_hora_fin IS NULL
          AND comunidad_id IS NULL AND evento_id IS NULL AND sancion_id IS NULL AND conexion_id IS NULL)
    OR (tipo = 'SANCION_ADVERTENCIA' AND sancion_id IS NOT NULL
          AND puente_fecha IS NULL AND puente_hora_inicio IS NULL AND puente_hora_fin IS NULL
          AND comunidad_id IS NULL AND evento_id IS NULL AND reporte_id IS NULL AND conexion_id IS NULL)
    OR (tipo IN ('CONEXION_SOLICITADA','CONEXION_ACEPTADA') AND conexion_id IS NOT NULL
          AND puente_fecha IS NULL AND puente_hora_inicio IS NULL AND puente_hora_fin IS NULL
          AND comunidad_id IS NULL AND evento_id IS NULL AND reporte_id IS NULL AND sancion_id IS NULL));
