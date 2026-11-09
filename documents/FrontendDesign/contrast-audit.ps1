[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$dir = 'D:\Studio\XTiger1986\Rampage.CryptedNotex\Rampage.CryptedNotex.Net\Rampage.CryptedNotex.WinForm\Internal\Themes'

function HexToRgb([string]$hex) {
    $h = $hex.TrimStart('#')
    return @([Convert]::ToInt32($h.Substring(0,2),16), [Convert]::ToInt32($h.Substring(2,2),16), [Convert]::ToInt32($h.Substring(4,2),16))
}
function ChanLum([double]$c) {
    if ($c -le 0.03928) { return $c / 12.92 }
    return [Math]::Pow(($c + 0.055) / 1.055, 2.4)
}
function RelLum([string]$hex) {
    $rgb = HexToRgb $hex
    return 0.2126*(ChanLum ($rgb[0]/255.0)) + 0.7152*(ChanLum ($rgb[1]/255.0)) + 0.0722*(ChanLum ($rgb[2]/255.0))
}
function Ratio([string]$a, [string]$b) {
    $la = RelLum $a; $lb = RelLum $b
    if ($la -lt $lb) { $t = $la; $la = $lb; $lb = $t }
    return ($la + 0.05) / ($lb + 0.05)
}

# fg, bg, label
$pairs = @(
    @('text','windowBack','text/window'),
    @('text','panelBack','text/panel'),
    @('text','panelAltBack','text/list'),
    @('text','inputBack','text/input'),
    @('text','selectionBack','text/selrow'),
    @('textSecondary','windowBack','muted/window'),
    @('textSecondary','panelBack','muted/panel'),
    @('textSecondary','panelAltBack','muted/list'),
    @('accentText','accent','btn/accent'),
    @('dangerText','danger','white/danger'),
    @('link','windowBack','link/window'),
    @('link','panelAltBack','link/list'),
    @('editorText','editorBack','editor'),
    @('trashTint','panelAltBack','trash/list'),
    @('success','panelAltBack','succ/toast'),
    @('dangerText','danger','dtext/danger')
)

foreach ($file in (Get-ChildItem $dir -Filter *.json | Sort-Object Name)) {
    $theme = Get-Content $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $c = $theme.colors
    Write-Output ('===== ' + $theme.id + ' (' + $theme.name + ')  previewBack=' + $c.previewBack + ' L=' + [Math]::Round((RelLum $c.previewBack),3))
    foreach ($p in $pairs) {
        $fg = $p[0]; $bg = $p[1]
        if ($fg -eq 'FFFFFF') { $fgHex = '#FFFFFF' } else { $fgHex = $c.$fg }
        if (-not $fgHex -or -not $c.$bg) { continue }
        $r = Ratio $fgHex $c.$bg
        $mark = '  '
        if ($r -lt 3.0) { $mark = 'XX' }
        elseif ($r -lt 4.5) { $mark = '--' }
        Write-Output ('  ' + $mark + ' ' + $p[2].PadRight(14) + ' ' + [Math]::Round($r,2))
    }
}
Write-Output ''
Write-Output 'XX = below 3.0 (fail)   -- = 3.0..4.5 (ok only for large/bold text)'
Write-Output ('accent alienmorph=' + ((Get-Content ($dir + '\alienmorph.json') -Raw -Encoding UTF8 | ConvertFrom-Json).colors.accent) + '  alx=' + ((Get-Content ($dir + '\alx.json') -Raw -Encoding UTF8 | ConvertFrom-Json).colors.accent))
