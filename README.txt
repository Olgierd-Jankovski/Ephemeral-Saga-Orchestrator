docker-compose build

cd k8s

kubectl apply -f rabbitmq-deployment.yaml

kubectl apply -f order-db-deployment.yaml
kubectl apply -f order-db-service.yaml

kubectl apply -f OrderService-deployment.yaml
kubectl apply -f OrderService-service.yaml

kubectl apply -f inventory-db-deployment.yaml
kubectl apply -f inventory-db-service.yaml

kubectl apply -f InventoryService-deployment.yaml
kubectl apply -f InventoryService-service.yaml

kubectl apply -f eso-db-deployment.yaml
kubectl apply -f eso-db-service.yaml

kubectl apply -f outbox-db-deployment.yaml
kubectl apply -f outbox-db-service.yaml

kubectl apply -f rbac.yaml
kubectl apply -f GatewayService-deployment.yaml
kubectl apply -f GatewayService-service.yaml

kubectl apply -f eso-job.yaml

### testing
kubectl apply -f k6-job.yaml
kubectl logs -f job/k6-loadtest

kubectl apply -f k6-job-choreo.yaml
kubectl logs -f job/k6-choreo-loadtest

kubectl apply -f k6-job-neso.yaml
kubectl logs -f job/k6-neso-loadtest

# Pod‑kill kas kelias s – taikoma TIK ESO Job’ams
kubectl apply -f chaos-kill-eso.yaml

###

kubectl delete all --all

kubectl get pods -o wide

kubectl describe pod <pod-name>


kubectl scale deployment order-deployment --replicas=0
kubectl scale deployment inventory-deployment --replicas=0
kubectl scale deployment gateway-deployment --replicas=0

kubectl scale deployment order-deployment --replicas=1
kubectl scale deployment inventory-deployment --replicas=1
kubectl scale deployment gateway-deployment --replicas=1
