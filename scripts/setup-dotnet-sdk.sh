#!/usr/bin/env bash
set -euo pipefail

SDK_CHANNEL="${1:-9.0}"
INSTALL_MODE="${2:-user}"
PERSIST_PROFILE="${DOTNET_PERSIST_PROFILE:-1}"
DOTNET_ROOT_DEFAULT="$HOME/.dotnet"
PROFILE_BLOCK_BEGIN="# >>> dotnet-sdk-path >>>"
PROFILE_BLOCK_END="# <<< dotnet-sdk-path <<<"

persist_dotnet_env() {
  local dotnet_root="$1"

  if [[ "${PERSIST_PROFILE}" != "1" ]]; then
    return
  fi

  local profile_targets=("$HOME/.bashrc" "$HOME/.profile")
  local profile_file
  for profile_file in "${profile_targets[@]}"; do
    touch "${profile_file}"

    if rg -q "${PROFILE_BLOCK_BEGIN}" "${profile_file}"; then
      continue
    fi

    {
      echo
      echo "${PROFILE_BLOCK_BEGIN}"
      echo "export DOTNET_ROOT=\"${dotnet_root}\""
      echo 'export PATH="$DOTNET_ROOT:$PATH"'
      echo "${PROFILE_BLOCK_END}"
    } >> "${profile_file}"
  done
}

wire_existing_dotnet() {
  local dotnet_root="$1"
  local dotnet_bin="${dotnet_root}/dotnet"

  if [[ ! -x "${dotnet_bin}" ]]; then
    return 1
  fi

  export DOTNET_ROOT="${dotnet_root}"
  export PATH="${DOTNET_ROOT}:${PATH}"

  persist_dotnet_env "${DOTNET_ROOT}"

  echo "dotnet already present at ${dotnet_bin}; using it now."
  "${dotnet_bin}" --info
  return 0
}

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "Unsupported OS for this helper: $(uname -s)"
  echo "Install .NET SDK manually: https://learn.microsoft.com/dotnet/core/install/"
  exit 1
fi

if command -v dotnet >/dev/null 2>&1; then
  echo "dotnet already present on PATH; leaving existing tooling unchanged."
  dotnet --info
  exit 0
fi

case "${INSTALL_MODE}" in
  user)
    DOTNET_ROOT="${DOTNET_ROOT:-$DOTNET_ROOT_DEFAULT}"
    DOTNET_INSTALL_SCRIPT="/tmp/dotnet-install.sh"

    if wire_existing_dotnet "${DOTNET_ROOT}"; then
      exit 0
    fi

    echo "Installing .NET SDK ${SDK_CHANNEL} to ${DOTNET_ROOT} (user-local mode)."
    wget -q https://dot.net/v1/dotnet-install.sh -O "${DOTNET_INSTALL_SCRIPT}"
    bash "${DOTNET_INSTALL_SCRIPT}" --channel "${SDK_CHANNEL}" --install-dir "${DOTNET_ROOT}" --quality ga
    rm -f "${DOTNET_INSTALL_SCRIPT}"

    export DOTNET_ROOT
    export PATH="${DOTNET_ROOT}:${PATH}"
    persist_dotnet_env "${DOTNET_ROOT}"

    echo
    echo "Configured for current shell."
    echo "Persisted DOTNET_ROOT/PATH to ~/.bashrc and ~/.profile (DOTNET_PERSIST_PROFILE=${PERSIST_PROFILE})."
    ;;
  global)
    if [[ ! -f /etc/os-release ]]; then
      echo "Cannot determine Linux distribution (/etc/os-release missing)."
      exit 1
    fi

    # shellcheck disable=SC1091
    source /etc/os-release

    if [[ "${ID:-}" != "ubuntu" ]]; then
      echo "Global mode currently supports Ubuntu only (detected: ${ID:-unknown})."
      echo "Use user mode instead: bash scripts/setup-dotnet-sdk.sh ${SDK_CHANNEL} user"
      exit 1
    fi

    if ! command -v sudo >/dev/null 2>&1; then
      echo "sudo is required for global mode."
      echo "Use user mode instead: bash scripts/setup-dotnet-sdk.sh ${SDK_CHANNEL} user"
      exit 1
    fi

    wget -q "https://packages.microsoft.com/config/ubuntu/${VERSION_ID}/packages-microsoft-prod.deb" -O /tmp/packages-microsoft-prod.deb
    sudo dpkg -i /tmp/packages-microsoft-prod.deb
    rm -f /tmp/packages-microsoft-prod.deb

    sudo apt-get update
    sudo apt-get install -y "dotnet-sdk-${SDK_CHANNEL}"
    ;;
  *)
    echo "Unsupported install mode: ${INSTALL_MODE}"
    echo "Usage: bash scripts/setup-dotnet-sdk.sh [channel] [user|global]"
    exit 1
    ;;
esac

dotnet --info
