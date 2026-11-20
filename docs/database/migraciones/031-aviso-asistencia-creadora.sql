SET NAMES utf8mb4;

-- Aviso a quien creó una comunidad cuando alguien confirma o se baja. Lleva una base con la 030 a la versión actual de
-- schema.sql. Se ejecuta UNA sola vez, con la base de datos ya seleccionada (sin USE). No hace falta en una base nueva
-- creada con el schema.sql actual. Volver a ejecutarla no rompe nada (reemplaza los mismos CHECK).
--
-- Qué cambia (solo los CHECK de notificacion; ninguna columna ni dato):
--   · ASISTENCIA_COMUNIDAD (con comunidad_id): «Ana se sumó a «jugar vóley» · van 3 de 6» o «Ana ya no va…». Le llega
--     a quien creó la comunidad. Es UN aviso por comunidad: uq_notif_comunidad (usuario, comunidad, tipo) hace que el
--     siguiente cambio reemplace el mensaje y lo vuelva a marcar como no leído, en vez de llenar la campanita.
-- Si la base no la tiene, el backend sigue funcionando: el aviso a quien creó simplemente no se guarda.

ALTER TABLE notificacion
  DROP CHECK ck_notif_tipo,
  DROP CHECK ck_notif_ref,
  ADD CONSTRAINT ck_notif_tipo CHECK (tipo IN ('PUENTE_DISPONIBLE','DESPUES_DE_CLASES','INVITACION_COMUNIDAD','COMUNIDAD_CERRADA',
                                           'EVENTO_APROBADO','EVENTO_RECHAZADO','EVENTO_BAJADO','REPORTE_RESUELTO','SANCION_ADVERTENCIA',
                                           'CONEXION_SOLICITADA','CONEXION_ACEPTADA','REPORTE_NUEVO','EVENTO_POR_REVISAR',
                                           'ASISTENCIA_COMUNIDAD')),
  ADD CONSTRAINT ck_notif_ref CHECK (
       (tipo IN ('PUENTE_DISPONIBLE','DESPUES_DE_CLASES')
          AND puente_fecha IS NOT NULL AND puente_hora_inicio IS NOT NULL AND puente_hora_fin IS NOT NULL
          AND comunidad_id IS NULL AND evento_id IS NULL AND reporte_id IS NULL AND sancion_id IS NULL AND conexion_id IS NULL)
    OR (tipo IN ('INVITACION_COMUNIDAD','COMUNIDAD_CERRADA','ASISTENCIA_COMUNIDAD') AND comunidad_id IS NOT NULL
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
