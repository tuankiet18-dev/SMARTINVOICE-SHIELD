$historyDirs = Get-ChildItem -Path "$env:APPDATA\Code\User\History" -Directory
$count = 0
foreach ($dir in $historyDirs) {
    $entriesPath = Join-Path $dir.FullName "entries.json"
    if (Test-Path $entriesPath) {
        $raw = Get-Content $entriesPath -Raw
        if ($raw -match "Semester_6_OJT_AWS.*SmartInvoice") {
            try {
                $json = ConvertFrom-Json -InputObject $raw
                if ($json.resource -match "SmartInvoice\.Frontend|inject|diff\.txt|Trash") {
                    $resPath = [System.Uri]::UnescapeDataString($json.resource)
                    if ($resPath.StartsWith("file:///")) {
                        $resPath = $resPath.Substring(8).Replace("/", "\")
                    } else {
                        continue
                    }
                    $latest = $json.entries[-1]
                    $backupPath = Join-Path $dir.FullName $latest.id
                    if (Test-Path $backupPath) {
                        $dirName = Split-Path $resPath -Parent
                        if (-not (Test-Path $dirName)) { New-Item -ItemType Directory -Path $dirName -Force | Out-Null }
                        Copy-Item -Path $backupPath -Destination $resPath -Force
                        Write-Host "Restored $resPath"
                        $count++
                    }
                }
            } catch {}
        }
    }
}
Write-Host "Total Restored: $count"
