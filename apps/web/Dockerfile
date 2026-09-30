FROM node:24-alpine
WORKDIR /app
RUN npm install --global pnpm@11.9.0
COPY package.json pnpm-workspace.yaml pnpm-lock.yaml ./
COPY apps/web/package.json apps/web/package.json
RUN pnpm install --frozen-lockfile
COPY apps/web apps/web
EXPOSE 5173
CMD ["pnpm", "--filter", "@clinica/web", "dev"]
