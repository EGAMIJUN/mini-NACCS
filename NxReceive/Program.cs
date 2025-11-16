using System;
using System.IO;

Console.WriteLine("=== NxReceive (SXGW 模擬受信) 開始 ===");

// DB と テーブル作成
DbHelper.EnsureSchema();

// 電文フォルダの指定
var inboxDir = "/Users/egamijun/NaccsInbox";
var processedDir = "/Users/egamijun/NaccsProcessed";

Directory.CreateDirectory(inboxDir);
Directory.CreateDirectory(processedDir);

// NACCS 電文ファイル一覧
var files = Directory.GetFiles(inboxDir, "*.txt");

if (files.Length == 0)
{
    Console.WriteLine("※ 受信ファイルなし");
    return;
}

foreach (var file in files)
{
    Console.WriteLine($"処理中ファイル: {file}");

    foreach (var line in File.ReadLines(file))
    {
        if (!line.StartsWith("DTL")) continue;

        var cols = line.Split(',');
        if (cols.Length < 7) continue;

        var msgType = cols[1];
        var vsl     = cols[2];
        var voy     = cols[3];
        var ctrNo   = cols[4];
        var pDate   = cols[5];
        var pTime   = cols[6];
        var now     = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        using var conn = DbHelper.GetConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
INSERT INTO T_NaxTantei
 (MessageType, VslCode, VoyNo, ContainerNo, PermitDate, PermitTime, Status, CreatedAt, UpdatedAt)
VALUES
 ($m,$v,$y,$c,$d,$t,'WAIT',$now,$now);
";

        cmd.Parameters.AddWithValue("$m", msgType);
        cmd.Parameters.AddWithValue("$v", vsl);
        cmd.Parameters.AddWithValue("$y", voy);
        cmd.Parameters.AddWithValue("$c", ctrNo);
        cmd.Parameters.AddWithValue("$d", pDate);
        cmd.Parameters.AddWithValue("$t", pTime);
        cmd.Parameters.AddWithValue("$now", now);

        cmd.ExecuteNonQuery();

        Console.WriteLine($"通知INSERT: {vsl}/{voy}/{ctrNo}");
    }

    // 処理済みフォルダへ移動
    var dest = Path.Combine(processedDir, Path.GetFileName(file));
    if (File.Exists(dest)) File.Delete(dest);
    File.Move(file, dest);

    Console.WriteLine($"→ 移動: {dest}");
}

Console.WriteLine("=== NxReceive 終了 ===");
