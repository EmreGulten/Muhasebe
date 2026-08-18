# Bağımlılıklar.
FROM node:24-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

# Build: standalone çıktı (next.config.ts'te output: "standalone").
# Rewrites build anında değerlendirildiği için API adresi burada bilinmeli.
FROM node:24-alpine AS build
WORKDIR /app
ARG API_PROXY_URL=http://localhost:5000
ENV API_PROXY_URL=${API_PROXY_URL}
COPY --from=deps /app/node_modules ./node_modules
COPY . .
ENV NEXT_TELEMETRY_DISABLED=1
RUN npm run build

# Runtime: standalone server + static dosyalar.
FROM node:24-alpine AS final
WORKDIR /app
ARG API_PROXY_URL=http://localhost:5000
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0 \
    API_PROXY_URL=${API_PROXY_URL}

COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public

USER node
EXPOSE 3000

CMD ["node", "server.js"]
