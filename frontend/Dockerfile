# syntax=docker/dockerfile:1
# ---- deps ----
FROM node:24-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

# ---- build ----
FROM node:24-alpine AS build
WORKDIR /app
ENV NEXT_TELEMETRY_DISABLED=1
# Version-skew protection for rolling updates: give every build its own id (e.g. git sha).
ARG NEXT_DEPLOYMENT_ID=local
ENV NEXT_DEPLOYMENT_ID=$NEXT_DEPLOYMENT_ID
COPY --from=deps /app/node_modules ./node_modules
COPY . .
# Same Server Actions key for every build/replica -> an action rendered by one pod can be
# executed by another, also across a rolling update. Passed as a BuildKit secret so it is not
# stored in image metadata (`docker history`). Next.js embeds it in the server bundle.
RUN --mount=type=secret,id=next_actions_key,env=NEXT_SERVER_ACTIONS_ENCRYPTION_KEY \
    if [ -z "$NEXT_SERVER_ACTIONS_ENCRYPTION_KEY" ]; then unset NEXT_SERVER_ACTIONS_ENCRYPTION_KEY; fi; \
    npm run build

# ---- runtime (standalone output, no dev dependencies) ----
FROM node:24-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0
COPY --from=build --chown=node:node /app/.next/standalone ./
COPY --from=build --chown=node:node /app/.next/static ./.next/static
COPY --from=build --chown=node:node /app/public ./public
USER node
EXPOSE 3000
CMD ["node", "server.js"]
