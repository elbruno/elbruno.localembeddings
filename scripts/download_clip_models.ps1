<#
.SYNOPSIS
Downloads the required CLIP ONNX models and tokenizer files for ElBruno.LocalEmbeddings.ImageEmbeddings.

.DESCRIPTION
This script downloads the following files from Hugging Face (Xenova/clip-vit-base-patch32):
- text_model.onnx
- vision_model.onnx
- vocab.json
- merges.txt

.PARAMETER OutputDirectory
The directory where the files will be saved. Default is ".\clip-models" relative to the current location.

.EXAMPLE
.\download_clip_models.ps1
Downloads files to .\clip-models

.EXAMPLE
.\download_clip_models.ps1 -OutputDirectory "C:\MyModels\CLIP"
Downloads files to the specified absolute path.
#>

param (
    [string]$OutputDirectory = ".\clip-models"
)

$BaseUrl = "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main"
$Files = @(
    @{ Name = "text_model.onnx"; Path = "onnx/text_model.onnx" },
    @{ Name = "vision_model.onnx"; Path = "onnx/vision_model.onnx" },
    @{ Name = "vocab.json"; Path = "vocab.json" },
    @{ Name = "merges.txt"; Path = "merges.txt" }
)

# Resolve path
$TargetDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)

Write-Host "preparing to download CLIP models to: $TargetDir" -ForegroundColor Cyan

if (!(Test-Path -Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
    Write-Host "Created directory: $TargetDir" -ForegroundColor Gray
}

foreach ($File in $Files) {
    $Url = "$BaseUrl/$($File.Path)"
    $OutputPath = Join-Path -Path $TargetDir -ChildPath $File.Name
    
    if (Test-Path -Path $OutputPath) {
        Write-Host "File already exists: $($File.Name) - Skipping" -ForegroundColor Yellow
        continue
    }

    Write-Host "Downloading $($File.Name)..." -ForegroundColor Green
    try {
        # Using Invoke-WebRequest with progress bar
        Invoke-WebRequest -Uri $Url -OutFile $OutputPath -ErrorAction Stop
        Write-Host "  Success" -ForegroundColor Green
    }
    catch {
        Write-Host "  Error downloading $($File.Name): $_" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`nAll files downloaded successfully to $TargetDir" -ForegroundColor Cyan
Write-Host "You can now use this directory with ElBruno.LocalEmbeddings.ImageEmbeddings." -ForegroundColor Gray
