#!/usr/bin/env bash
# Instala a versao portatil no perfil do usuario. Nao usa sudo.
set -euo pipefail

source_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
app_dir="${HOME}/.local/share/TradutorModsDeck"
applications_dir="${HOME}/.local/share/applications"
desktop_dir="${HOME}/Desktop"
launcher="${applications_dir}/tradutor-mods-deck.desktop"
desktop_shortcut="${desktop_dir}/Transferencia de Arquivos de Traducoes de Mods.desktop"

mkdir -p "$app_dir" "$applications_dir" "$desktop_dir"
cp -a "$source_dir"/. "$app_dir"/
chmod +x "$app_dir/TradutorModsDeck"

cat > "$launcher" <<EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=Valley Modkeeper
Comment=Gerencie traduções e configurações dos mods de Stardew Valley
Exec=$app_dir/TradutorModsDeck
Path=$app_dir
Icon=$app_dir/Assets/app.png
Terminal=false
Categories=Utility;Game;
StartupNotify=true
EOF

cp -f "$launcher" "$desktop_shortcut"
chmod +x "$launcher" "$desktop_shortcut"

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$applications_dir" >/dev/null 2>&1 || true
fi

echo
echo "Instalacao concluida!"
echo "Abra o Valley Modkeeper pelo icone na Area de Trabalho ou pelo menu de aplicativos."
