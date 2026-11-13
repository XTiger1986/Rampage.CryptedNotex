param(
    [string]$ThemeDirectory = (Join-Path $PSScriptRoot '..\..\Rampage.CryptedNotex.Net\Rampage.CryptedNotex.WinForm\Internal\Themes')
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'

function Get-ThemeColor([psobject]$Colors, [string]$Key) {
    $property = $Colors.PSObject.Properties[$Key]
    if (-not $property -or -not $property.Value) {
        # 新增语义底色与运行时采用相同的旧主题回退规则。
        if ($Key -eq 'sidebarBack') { return Get-ThemeColor $Colors 'panelBack' }
        if ($Key -eq 'cardBack') { return Get-ThemeColor $Colors 'panelAltBack' }
        throw ('Missing required color: ' + $Key)
    }
    $hex = [string]$property.Value
    if ($hex -notmatch '^#[0-9A-Fa-f]{6}$') {
        throw ('Invalid #RRGGBB color: ' + $Key + '=' + $hex)
    }
    return $hex
}

function Get-LinearChannel([double]$Value) {
    if ($Value -le 0.04045) { return $Value / 12.92 }
    return [Math]::Pow(($Value + 0.055) / 1.055, 2.4)
}

function Get-RelativeLuminance([string]$Hex) {
    $red = [Convert]::ToInt32($Hex.Substring(1, 2), 16) / 255.0
    $green = [Convert]::ToInt32($Hex.Substring(3, 2), 16) / 255.0
    $blue = [Convert]::ToInt32($Hex.Substring(5, 2), 16) / 255.0
    return 0.2126 * (Get-LinearChannel $red) + 0.7152 * (Get-LinearChannel $green) + 0.0722 * (Get-LinearChannel $blue)
}

function Get-ContrastRatio([string]$Foreground, [string]$Background) {
    $first = Get-RelativeLuminance $Foreground
    $second = Get-RelativeLuminance $Background
    return ([Math]::Max($first, $second) + 0.05) / ([Math]::Min($first, $second) + 0.05)
}

# 仅审计文字与实际承载底色；装饰图案和分类色点不作为正文文字。
$pairs = @(
    @('text', 'windowBack'),
    @('text', 'panelBack'),
    @('text', 'panelAltBack'),
    @('text', 'inputBack'),
    @('text', 'selectionBack'),
    @('text', 'sidebarBack'),
    @('text', 'cardBack'),
    @('textSecondary', 'windowBack'),
    @('textSecondary', 'panelBack'),
    @('textSecondary', 'panelAltBack'),
    @('textSecondary', 'inputBack'),
    @('textSecondary', 'selectionBack'),
    @('textSecondary', 'sidebarBack'),
    @('textSecondary', 'cardBack'),
    @('accentText', 'accent'),
    @('dangerText', 'danger'),
    @('link', 'windowBack'),
    @('link', 'panelAltBack'),
    @('editorText', 'editorBack'),
    @('editorText', 'previewBack'),
    @('trashTint', 'panelAltBack'),
    @('trashTint', 'selectionBack'),
    @('success', 'panelAltBack'),
    @('danger', 'panelAltBack'),
    @('accent', 'panelBack'),
    @('accent', 'panelAltBack')
)

$checked = 0
$failed = 0
$themeCount = 0
try {
    $files = @(Get-ChildItem -LiteralPath $ThemeDirectory -Filter '*.json' -File | Sort-Object Name)
    if ($files.Count -eq 0) { throw ('No theme JSON files found: ' + $ThemeDirectory) }
    foreach ($file in $files) {
        try {
            $theme = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
            if (-not $theme.id -or -not $theme.colors) { throw 'Theme requires id and colors.' }
            $themeCount++
            $minimum = [double]::MaxValue
            $themeFailed = 0
            foreach ($pair in $pairs) {
                $checked++
                try {
                    $foreground = Get-ThemeColor $theme.colors $pair[0]
                    $background = Get-ThemeColor $theme.colors $pair[1]
                    $ratio = Get-ContrastRatio $foreground $background
                    $minimum = [Math]::Min($minimum, $ratio)
                    if ($ratio -lt 4.5) {
                        $failed++
                        $themeFailed++
                        Write-Output ('[FAIL] {0}: {1}/{2} = {3:F3} (requires 4.5)' -f $theme.id, $pair[0], $pair[1], $ratio)
                    }
                }
                catch {
                    $failed++
                    $themeFailed++
                    Write-Output ('[FAIL] {0}: {1}/{2}: {3}' -f $theme.id, $pair[0], $pair[1], $_.Exception.Message)
                }
            }
            $status = if ($themeFailed -eq 0) { 'PASS' } else { 'FAIL' }
            Write-Output ('[{0}] {1} ({2}): {3} pairs, minimum {4:F3}' -f $status, $theme.id, $theme.name, $pairs.Count, $minimum)
        }
        catch {
            $failed++
            Write-Output ('[FAIL] {0}: {1}' -f $file.Name, $_.Exception.Message)
        }
    }
}
catch {
    $failed++
    Write-Output ('[FAIL] ' + $_.Exception.Message)
}

Write-Output ('Themes: {0}; pairs checked: {1}; failures: {2}; WCAG AA threshold: 4.5' -f $themeCount, $checked, $failed)
if ($failed -gt 0) { exit 1 }
exit 0
