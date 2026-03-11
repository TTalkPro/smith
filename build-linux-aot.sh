#!/bin/bash
# Smith - Linux AOT Build Script
# Builds a native Linux x64 binary using .NET AOT compilation

set -e  # Exit on error

echo "=== Smith Linux AOT Build ==="
echo ""

# Configuration
PROJECT_PATH="src/Smith/Smith.csproj"
OUTPUT_DIR="publish/linux-x64"
RUNTIME="linux-x64"
CONFIGURATION="Release"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if .NET SDK is installed
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}Error: .NET SDK is not installed${NC}"
    echo "Please install .NET 10.0 SDK or later"
    exit 1
fi

# Display .NET version
echo -e "${YELLOW}.NET Version:${NC}"
dotnet --version
echo ""

# Clean previous builds
echo -e "${YELLOW}Cleaning previous builds...${NC}"
rm -rf "$OUTPUT_DIR"
echo ""

# Restore dependencies
echo -e "${YELLOW}Restoring dependencies...${NC}"
dotnet restore "$PROJECT_PATH"
echo ""

# Build and publish self-contained (no trimming, works with Spectre.Console.Cli)
echo -e "${YELLOW}Building self-contained binary for $RUNTIME...${NC}"
dotnet publish "$PROJECT_PATH" \
    --runtime "$RUNTIME" \
    --configuration "$CONFIGURATION" \
    --output "$OUTPUT_DIR" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=none \
    -p:DebugSymbols=false

echo ""

# Check if build succeeded
if [ -f "$OUTPUT_DIR/smith" ]; then
    echo -e "${GREEN}✓ Build successful!${NC}"
    echo ""
    echo -e "${YELLOW}Binary location:${NC} $OUTPUT_DIR/smith"
    echo -e "${YELLOW}Binary size:${NC} $(du -h "$OUTPUT_DIR/smith" | cut -f1)"
    echo ""
    echo -e "${YELLOW}To run:${NC}"
    echo "  cd $OUTPUT_DIR"
    echo "  ./smith --help"
    echo ""
    echo -e "${YELLOW}To install system-wide:${NC}"
    echo "  sudo cp $OUTPUT_DIR/smith /usr/local/bin/"
else
    echo -e "${RED}✗ Build failed!${NC}"
    exit 1
fi
