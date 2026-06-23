# ============================================
# MURO Admin Frontend — Production Dockerfile
# Two-stage: build → runner
# ============================================

# --- Stage 1: Build ---
FROM node:20-alpine AS builder
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm install

# Diagnostics: Check if next is installed before COPY
RUN echo "=== DIAGNOSTIC: BEFORE COPY ===" && \
    ls -la node_modules/next/dist/bin/next || echo "next binary NOT found before COPY"

COPY . .

# Diagnostics: Check if next is installed after COPY
RUN echo "=== DIAGNOSTIC: AFTER COPY ===" && \
    ls -la node_modules/next/dist/bin/next || echo "next binary NOT found after COPY"

# Build-time environment variables
ARG NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_API_URL=${NEXT_PUBLIC_API_URL}

RUN npm run build

# --- Stage 2: Runner ---
FROM node:20-alpine AS runner
WORKDIR /app

ENV NODE_ENV=production
ENV PORT=3001

# Create non-root user
RUN addgroup --system --gid 1001 nodejs && \
    adduser --system --uid 1001 nextjs

# Copy standalone output
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static
COPY --from=builder /app/public ./public

USER nextjs

EXPOSE 3001

HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD wget -q --spider http://localhost:3001 || exit 1

CMD ["node", "server.js"]
