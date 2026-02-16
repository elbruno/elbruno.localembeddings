#!/bin/bash

# Defaults
OUTPUT_DIR="./clip-models"

# Parse arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        -o|--output) OUTPUT_DIR="$2"; shift ;;
        *) echo "Unknown parameter passed: $1"; exit 1 ;;
    esac
    shift
done

BASE_URL="https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main"

# Declare files (Associative arrays used for mapping remote path to local name if needed, 
# but simple arrays work since we just map structure manually or keep it flat)
# Using flat structure for local dir as expected by library.

# Define files: "RemoteRelativePath|LocalFileName"
FILES=(
    "onnx/text_model.onnx|text_model.onnx"
    "onnx/vision_model.onnx|vision_model.onnx"
    "vocab.json|vocab.json"
    "merges.txt|merges.txt"
)

echo "Preparing to download CLIP models to: $OUTPUT_DIR"

mkdir -p "$OUTPUT_DIR"

for entry in "${FILES[@]}"; do
    remote_path="${entry%%|*}"
    local_name="${entry##*|}"
    
    url="${BASE_URL}/${remote_path}"
    output_path="${OUTPUT_DIR}/${local_name}"

    if [ -f "$output_path" ]; then
        echo "File already exists: $local_name - Skipping"
    else
        echo "Downloading $local_name..."
        if command -v curl >/dev/null 2>&1; then
            curl -L "$url" -o "$output_path" --fail
        elif command -v wget >/dev/null 2>&1; then
            wget "$url" -O "$output_path"
        else
            echo "Error: Neither curl nor wget found."
            exit 1
        fi
        
        if [ $? -ne 0 ]; then
            echo "Error downloading $local_name"
            exit 1
        fi
        echo "  Success"
    fi
done

echo ""
echo "All files downloaded successfully to $OUTPUT_DIR"
echo "You can now use this directory with ElBruno.LocalEmbeddings.ImageEmbeddings."
