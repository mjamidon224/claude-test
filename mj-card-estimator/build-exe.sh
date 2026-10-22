#!/usr/bin/env bash
# Builds dist/JordanCardAppraiser.exe (Windows x64) from any OS with Go installed.
# Set GOARCH=arm64 for Windows on Arm.
set -euo pipefail
cd "$(dirname "$0")"
arch="${GOARCH:-amd64}"
go run github.com/tc-hib/go-winres@v0.3.3 make --arch "$arch"
mkdir -p dist
GOOS=windows GOARCH="$arch" CGO_ENABLED=0 go build -trimpath -ldflags "-H windowsgui -s -w" -o dist/JordanCardAppraiser.exe .
echo "Built $(pwd)/dist/JordanCardAppraiser.exe"
