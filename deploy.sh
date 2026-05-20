#!/usr/bin/env bash
set -euo pipefail

echo "Downloading Images..."
docker pull postgres:18
docker pull rabbitmq:3-management
minikube image load postgres:18
minikube image load rabbitmq:3-management

echo "Deploying Queue..."
kubectl apply -f queue/deploy.yaml

echo "Deploying Database..."
kubectl create configmap postgres-schema --from-file=schema.sql=database/schema.sql --dry-run=client -o yaml | kubectl apply -f -
kubectl apply -f database/deploy.yaml
kubectl wait --for=condition=ready pod -l app=postgres --timeout=300s

echo "Publishing and Deploying API..."
dotnet publish ./src/MeterSystem.Api/MeterSystem.Api.csproj -t:PublishContainer
minikube image load metersystem-api:latest
kubectl apply -f ./src/MeterSystem.Api/deploy.yaml

echo "Deploying Worker..."
dotnet publish ./src/MeterSystem.Worker/MeterSystem.Worker.csproj -t:PublishContainer
minikube image load metersystem-worker:latest
kubectl apply -f ./src/MeterSystem.Worker/deploy.yaml

echo "done"
