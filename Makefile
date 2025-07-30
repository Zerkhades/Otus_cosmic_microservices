.PHONY: build run stop clean logs

# Build all services
build:
	docker-compose build

# Run all services
run:
	docker-compose up -d

# Stop all services
stop:
	docker-compose down

# Stop all services and remove volumes
clean:
	docker-compose down -v

# View logs for all services
logs:
	docker-compose logs -f

# View logs for a specific service
# Usage: make service-logs service=player-service
service-logs:
	docker-compose logs -f $(service)

# Run just the infrastructure
infra:
	docker-compose up -d postgres mongo kafka zookeeper

# Rebuild and restart a specific service
# Usage: make restart-service service=player-service
restart-service:
	docker-compose up -d --build $(service)

# Show status of all services
status:
	docker-compose ps