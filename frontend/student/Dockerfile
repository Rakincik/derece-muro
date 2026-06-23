# ============================================
# MURO Student Frontend — Production Dockerfile
# Two-stage: build → runner
# ============================================

# --- Stage 1: Build ---
FROM node:20-alpine AS builder
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm install

RUN echo "Listing node_modules/next/dist/bin:" && ls -la node_modules/next/dist/bin/
RUN echo "Listing node_modules/.bin:" && ls -la node_modules/.bin/
RUN exit 1

# Build-time environment variables
ARG NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_API_URL=${NEXT_PUBLIC_API_URL}

RUN npm run build

# --- Stage 2: Runner ---
FROM node:20-alpine AS runner
WORKDIR /app

ENV NODE_ENV=production
ENV PORT=3002

# Create non-root user
RUN addgroup --system --gid 1001 nodejs && \
    adduser --system --uid 1001 nextjs

# Copy standalone output
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static
COPY --from=builder /app/public ./public

USER nextjs

EXPOSE 3002

HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD wget -q --spider http://localhost:3002 || exit 1

CMD ["node", "server.js"]
