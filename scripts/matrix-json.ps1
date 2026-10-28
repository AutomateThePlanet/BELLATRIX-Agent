# ConvertTo-MatrixJson, shared by scripts/conformance-report.ps1 and scripts/export-website-data.ps1 (dot-source it).
# docs/support-matrix.json is written with 2-space indentation (the layout of Python's json.dumps(indent=2,
# ensure_ascii=False)), so an evidence update changes only the evidence lines. PS 5.1 ConvertTo-Json would reformat it.
function ConvertTo-MatrixJson($value, [int]$level = 0) {
    if ($null -eq $value) { return "null" }
    if ($value -is [bool]) { return $(if ($value) { "true" } else { "false" }) }
    if ($value -is [string]) {
        $sb = New-Object System.Text.StringBuilder
        [void]$sb.Append([char]34)
        foreach ($ch in $value.ToCharArray()) {
            $code = [int]$ch
            if ($code -eq 34 -or $code -eq 92) { [void]$sb.Append([char]92).Append($ch) }
            elseif ($code -eq 10) { [void]$sb.Append([char]92).Append('n') }
            elseif ($code -eq 13) { [void]$sb.Append([char]92).Append('r') }
            elseif ($code -eq 9) { [void]$sb.Append([char]92).Append('t') }
            elseif ($code -lt 32) { [void]$sb.Append([char]92).Append('u').Append($code.ToString("x4")) }
            else { [void]$sb.Append($ch) }
        }
        [void]$sb.Append([char]34)
        return $sb.ToString()
    }
    if ($value -is [ValueType]) { return ([IFormattable]$value).ToString($null, [Globalization.CultureInfo]::InvariantCulture) }
    $pad = "  " * ($level + 1); $close = "  " * $level
    if ($value -is [Management.Automation.PSCustomObject]) {
        $props = @($value.PSObject.Properties)
        if (-not $props.Count) { return "{}" }
        $items = foreach ($p in $props) { $pad + (ConvertTo-MatrixJson $p.Name) + ": " + (ConvertTo-MatrixJson $p.Value ($level + 1)) }
        return "{`n" + ($items -join ",`n") + "`n$close}"
    }
    $array = @($value)
    if (-not $array.Count) { return "[]" }
    $items = foreach ($v in $array) { $pad + (ConvertTo-MatrixJson $v ($level + 1)) }
    return "[`n" + ($items -join ",`n") + "`n$close]"
}
