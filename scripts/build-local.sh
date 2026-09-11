#!/usr/bin/env bash
set -e

# 获取脚本所在根目录
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
cd "$ROOT_DIR"

echo "========================================="
echo "   SysMonitor Linux Native Build Script  "
echo "========================================="

# 探测系统架构
ARCH=$(uname -m)
case "$ARCH" in
    x86_64)
        RID="linux-x64"
        ;;
    aarch64|arm64)
        RID="linux-arm64"
        ;;
    *)
        echo "[WARN] Unknown architecture '$ARCH', defaulting to linux-x64"
        RID="linux-x64"
        ;;
esac

echo "[1/3] Target Architecture: $ARCH ($RID)"

# 检查 clang 和 dotnet 是否存在
if ! command -v dotnet &> /dev/null; then
    echo "[ERROR] dotnet SDK 8.0+ is required to compile."
    exit 1
fi

if ! command -v clang &> /dev/null; then
    echo "[WARN] clang is recommended for Native AOT. Attempting standard build..."
fi

echo "[2/3] Compiling Native AOT executable..."
mkdir -p dist

dotnet publish SysMonitor.csproj \
    -r "$RID" \
    -c Release \
    -p:PublishAot=true \
    -p:PublishSingleFile=true \
    -p:StripSymbols=true \
    -o "dist/$RID"

echo "[3/3] Build completed successfully!"
ls -lh "dist/$RID/sysmonitor"

echo ""
echo "To run immediately:"
echo "  ./dist/$RID/sysmonitor"
