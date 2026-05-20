# Meter System

A backend system for receiving and processing meter readings asynchronously.

**Flow:** Client → REST API → RabbitMQ → Worker → PostgreSQL

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Minikube](https://minikube.sigs.k8s.io/docs/start/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git Bash (included with [Git for Windows](https://git-scm.com/))

## Running the System

```bash
minikube start
bash deploy.sh
```

The script will:
1. Pull and load Docker images into Minikube
2. Deploy RabbitMQ and PostgreSQL
3. Apply the database schema automatically via ConfigMap
4. Build and deploy the API and Worker

## Accessing the Services

Each command below opens a tunnel — **keep the terminal open** while using the service.

**API:**
```bash
minikube service metersystem-api
```
Navigate to `/swagger` to use the interactive UI.

**RabbitMQ Management UI:**
```bash
minikube service rabbitmq
```
Open the second URL shown in the terminal. Login with `guest` / `guest`.

## Testing

**Standard endpoint** — POST to `/api/readings`:

```json
{
  "meter_number": 12345,
  "readings": {
    "2026-03-18T10:15:00Z": 1234.56,
    "2026-03-18T10:00:00Z": 1234.51
  }
}
```

**Raw protobuf endpoint** — POST to `/api/readings/raw`:

```json
{
  "meter_number": 12345,
  "data": "ChEKBgik9unNBhEK16NwPUqTQAoRCgYIoO/pzQYR16NwPQpKk0A="
}
```

A `202 Accepted` response means the reading was queued successfully.

## Verifying Data in PostgreSQL

```bash
kubectl exec -it deployment/postgres -- psql -U postgres -d meters -c "SELECT * FROM meters; SELECT * FROM meter_readings;"
```

## Design Notes

- **MeterSystem.Api** — chose a controller-based API over Minimal APIs for better readability, structure, and maintainability

- **deploy.sh** — replaced `minikube image pull` with `docker pull` + `minikube image load` because when using the Docker driver on Windows, Minikube containers may not always have external internet access; loading images through Docker is more reliable

- **Database schema** — applied automatically on every postgres startup via a ConfigMap mounted at `/docker-entrypoint-initdb.d/`; no manual schema step required. This works for local development because the pod has no persistent storage and starts fresh on every restart. In production, where the database already exists, this directory is skipped by postgres — a proper migration tool (e.g. Flyway, Liquibase) should be used instead

- **RabbitMQ** — exposed as NodePort to allow direct access to the management UI via `minikube service rabbitmq`

- **MeterReadingWorker** — uses `ON CONFLICT DO NOTHING` when inserting readings to ensure idempotency; readings with the same meter and timestamp are ignored if already inserted

- **Message acknowledgment** — the Worker only ACKs a message after a successful DB save; on failure it NACKs with `requeue: true` so the message returns to the queue

- **Shared MeterData model** — used a single shared model between the API and Worker services. A separate HTTP DTO layer could have been introduced, but since both structures are currently identical, a shared model kept the implementation simpler and easier to maintain

- **POST /api/readings/raw** — accepts a JSON payload containing a Base64-encoded protobuf message. The protobuf schema is defined in `Protos/meter_data.proto`, and the corresponding C# classes are generated automatically at build time using `Grpc.Tools`. After decoding and deserialization, the readings are converted into `MeterData` and published to the same RabbitMQ queue used by the standard endpoint.

## Architecture Diagram

![System Diagram](task-diagram.png)
