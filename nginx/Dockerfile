# Config baked into the image: identical in docker-compose and Kubernetes.
FROM nginx:1.29-alpine
COPY nginx.conf /etc/nginx/nginx.conf
# Empty in production; docker-compose.dev.yml mounts nginx/dev/ here.
RUN mkdir -p /etc/nginx/extra
