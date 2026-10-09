#!/bin/sh
# Cogworks installer for macOS and Linux.
#   curl -fsSL https://raw.githubusercontent.com/wumgub/cogworks/main/install.sh | sh
# Downloads the latest release, installs it for the current user (no sudo) and launches it.
set -e

REPO="wumgub/cogworks"
BASE="https://github.com/$REPO/releases/latest/download"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

case "$(uname -s)" in
  Darwin)
    echo "Downloading Cogworks for macOS..."
    curl -fL --progress-bar "$BASE/Cogworks-macOS.zip" -o "$TMP/cogworks.zip"
    mkdir -p "$HOME/Applications"
    rm -rf "$HOME/Applications/Cogworks.app"
    ditto -x -k "$TMP/cogworks.zip" "$HOME/Applications"
    # The app isn't notarized by Apple; clear the download quarantine so it opens without a warning.
    xattr -dr com.apple.quarantine "$HOME/Applications/Cogworks.app" 2>/dev/null || true
    echo "Installed to ~/Applications/Cogworks.app"
    open "$HOME/Applications/Cogworks.app"
    ;;
  Linux)
    DEST="$HOME/.local/share/cogworks"
    echo "Downloading Cogworks for Linux..."
    curl -fL --progress-bar "$BASE/Cogworks-Linux.tar.gz" -o "$TMP/cogworks.tar.gz"
    rm -rf "$DEST"
    mkdir -p "$DEST" "$HOME/.local/bin" "$HOME/.local/share/applications"
    tar -xzf "$TMP/cogworks.tar.gz" -C "$DEST"
    chmod +x "$DEST/Cogworks.x86_64"
    ln -sf "$DEST/Cogworks.x86_64" "$HOME/.local/bin/cogworks"
    cat > "$HOME/.local/share/applications/cogworks.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Cogworks
Comment=Co-op pixel-art factory automation
Exec=$DEST/Cogworks.x86_64
Path=$DEST
Icon=$DEST/icon.png
Terminal=false
Categories=Game;Simulation;
DESKTOP
    echo "Installed to $DEST"
    echo "Launch it from your app menu, or run: cogworks   (needs ~/.local/bin on your PATH)"
    ;;
  *)
    echo "This script supports macOS and Linux. On Windows, run in PowerShell:"
    echo "  irm https://raw.githubusercontent.com/$REPO/main/install.ps1 | iex"
    exit 1
    ;;
esac
