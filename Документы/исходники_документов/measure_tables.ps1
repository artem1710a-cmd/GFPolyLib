param([string]$Docx, [string]$Out)
# Обновляет поля и содержание, затем выписывает номер страницы начала каждой строки каждой таблицы.
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try {
    $doc = $word.Documents.Open($Docx)
    $doc.Fields.Update() | Out-Null
    foreach ($toc in $doc.TablesOfContents) { $toc.Update() }
    $doc.Repaginate()
    foreach ($toc in $doc.TablesOfContents) { $toc.Update() }
    $doc.Repaginate()
    $lines = New-Object System.Collections.Generic.List[string]
    $ti = 0
    foreach ($t in $doc.Tables) {
        $pages = @()
        foreach ($row in $t.Rows) {
            $pages += $doc.Range($row.Range.Start, $row.Range.Start).Information(3)
        }
        $lines.Add("$ti " + ($pages -join ","))
        $ti++
    }
    [System.IO.File]::WriteAllLines($Out, $lines)
    $doc.Close([ref]0)
} finally {
    $word.Quit()
}
