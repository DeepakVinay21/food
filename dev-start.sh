#!/usr/bin/env bash
set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${GREEN}=== Food Expiration Tracker - Dev Startup ===${NC}"

# --- Port conflict detection ---
check_port() {
  local port=$1
  local name=$2
  if lsof -ti:"$port" &>/dev/null; then
    echo -e "${YELLOW}Port $port is already in use ($name).${NC}"
    read -p "Kill existing process? [y/N] " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
      lsof -ti:"$port" | xargs kill -9 2>/dev/null || true
      sleep 1
      echo -e "${GREEN}Killed process on port $port${NC}"
    else
      echo -e "${RED}Cannot start $name - port $port in use${NC}"
      exit 1
    fi
  fi
}

# 1. Check SQL Server connectivity
echo -n "Checking SQL Server... "
if command -v sqlcmd &>/dev/null; then
  if sqlcmd -S localhost,1433 -U sa -P 'Vinay@123Strong' -Q "SELECT 1" -b &>/dev/null; then
    echo -e "${GREEN}OK${NC}"
  else
    echo -e "${RED}FAILED${NC}"
    echo -e "${YELLOW}SQL Server not reachable on localhost:1433. Start it with:${NC}"
    echo "  docker run -e 'ACCEPT_EULA=Y' -e 'MSSQL_SA_PASSWORD=Vinay@123Strong' -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest"
    exit 1
  fi
else
  echo -e "${YELLOW}sqlcmd not installed, checking port...${NC}"
  if nc -z localhost 1433 2>/dev/null; then
    echo -e "${GREEN}Port 1433 is open${NC}"
  else
    echo -e "${RED}SQL Server not found on port 1433${NC}"
    echo "  docker run -e 'ACCEPT_EULA=Y' -e 'MSSQL_SA_PASSWORD=Vinay@123Strong' -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest"
    exit 1
  fi
fi

# 2. Check PaddleOCR service (optional)
echo -n "Checking PaddleOCR service... "
if nc -z localhost 8090 2>/dev/null; then
  echo -e "${GREEN}OK (port 8090)${NC}"
else
  echo -e "${YELLOW}Not running (optional - Gemini Vision handles OCR)${NC}"
fi

# 3. Check for port conflicts before starting
check_port 5001 "backend"
check_port 8080 "frontend"

# 4. Run EF Core migrations
echo "Running database migrations..."
cd "$(dirname "$0")"
dotnet ef database update --project backend/FoodExpirationTracker.Infrastructure --startup-project backend/FoodExpirationTracker.Api 2>&1 || echo -e "${YELLOW}Migration skipped (will run on startup)${NC}"

# 5. Start backend with retry logic
echo -e "${GREEN}Starting backend on port 5001...${NC}"
BACKEND_RETRIES=0
MAX_RETRIES=3
BACKEND_PID=""
while [ $BACKEND_RETRIES -lt $MAX_RETRIES ]; do
  dotnet run --project backend/FoodExpirationTracker.Api &
  BACKEND_PID=$!
  sleep 3
  if kill -0 "$BACKEND_PID" 2>/dev/null; then
    echo -e "${GREEN}Backend started (PID: $BACKEND_PID)${NC}"
    break
  fi
  BACKEND_RETRIES=$((BACKEND_RETRIES + 1))
  echo -e "${YELLOW}Backend failed to start, retrying ($BACKEND_RETRIES/$MAX_RETRIES)...${NC}"
done
if [ $BACKEND_RETRIES -eq $MAX_RETRIES ]; then
  echo -e "${RED}Backend failed after $MAX_RETRIES attempts${NC}"
  exit 1
fi

# 6. Start frontend with dependency check
echo -e "${GREEN}Starting frontend...${NC}"
cd frontend

# Verify pnpm can resolve packages before starting
if ! corepack pnpm install --frozen-lockfile 2>/dev/null; then
  echo -e "${YELLOW}pnpm frozen install failed. Retrying with regular install...${NC}"
  if ! corepack pnpm install 2>&1; then
    echo -e "${RED}pnpm install failed. Check network and registry settings.${NC}"
    echo "  Try: npm config set registry https://registry.npmjs.org/"
    kill "$BACKEND_PID" 2>/dev/null
    exit 1
  fi
fi

corepack pnpm dev &
FRONTEND_PID=$!

echo -e "${GREEN}=== Dev servers started ===${NC}"
echo "  Backend PID: $BACKEND_PID (http://localhost:5001)"
echo "  Frontend PID: $FRONTEND_PID (http://localhost:8080)"
echo "Press Ctrl+C to stop all"

trap "kill $BACKEND_PID $FRONTEND_PID 2>/dev/null; exit" SIGINT SIGTERM
wait
