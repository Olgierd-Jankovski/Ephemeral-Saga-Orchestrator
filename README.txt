docker-compose build

cd k8s
kubectl apply -f order-db-deployment.yaml
kubectl apply -f order-db-service.yaml

kubectl apply -f OrderService-deployment.yaml
kubectl apply -f OrderService-service.yaml

kubectl apply -f payment-db-deployment.yaml
kubectl apply -f payment-db-service.yaml

kubectl apply -f PaymentService-deployment.yaml
kubectl apply -f PaymentService-service.yaml

kubectl apply -f shipping-db-deployment.yaml
kubectl apply -f shipping-db-service.yaml

kubectl apply -f ShippingService-deployment.yaml
kubectl apply -f ShippingService-service.yaml

kubectl apply -f inventory-db-deployment.yaml
kubectl apply -f inventory-db-service.yaml

kubectl apply -f InventoryService-deployment.yaml
kubectl apply -f InventoryService-service.yaml




kubectl apply -f eso-db-deployment.yaml
kubectl apply -f eso-db-service.yaml
kubectl apply -f eso-job.yaml



kubectl delete all --all

kubectl get pods -o wide

kubectl describe pod <pod-name>


kubectl scale deployment order-deployment --replicas=0
kubectl scale deployment payment-deployment --replicas=0
kubectl scale deployment shipping-deployment --replicas=0
kubectl scale deployment inventory-deployment --replicas=0

kubectl scale deployment order-deployment --replicas=1
kubectl scale deployment payment-deployment --replicas=1
kubectl scale deployment shipping-deployment --replicas=1
kubectl scale deployment inventory-deployment --replicas=1