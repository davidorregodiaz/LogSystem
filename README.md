# 📝 LogService

## 🎯 Objetivo

Permitir que distintas aplicaciones registren logs de forma centralizada y puedan consultarlos mediante una API, facilitando el monitoreo y la trazabilidad de errores o eventos importantes.

## 🏗️ Arquitectura del Proyecto

- **API**: expone endpoints HTTP para registrar y consultar logs.
- **Application**: contiene los casos de uso y lógica de negocio.
- **Domain**: define las entidades (por ejemplo LogEntry) y contratos (interfaces como ILogRepository).
- **Infrastructure**: implementación de la persistencia en base de datos (por ahora SQL Server), y otras dependencias externas.

## 🚀 Tecnologías utilizadas

- ASP.NET Core 8
- Entity Framework Core
- SQL Server

📤 Endpoints principales:

➕ Obtener logs:
      Get api/v1/logs
      Content-Type: application/json
      {
        "level": "Error",
        "message": "Excepción al guardar el pedido",
        "source": "OrderService",
        "timestamp": "2025-04-07T10:45:00Z"
      }

🔍 Consultar log por id
GET api/v1/logs?id=1
