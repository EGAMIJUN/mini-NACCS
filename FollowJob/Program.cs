using System;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== FollowJob (後続JOB) 開始 ===");

        // 念のためテーブル作成
        DbHelper.EnsureSchema();

        // ★ テスト用コンテナを1件だけ入れておく（VSL001/V001/CNT001）
        DbHelper.SeedSampleContainer();

        // ① 未処理通知を取得
        var pending = GetPendingNotifications();
        Console.WriteLine($"未処理通知件数: {pending.Count}");

        if (pending.Count == 0)
        {
            Console.WriteLine("処理対象なし。終了します。");
            return;
        }

        // ②〜⑤ 通知ごとに処理
        foreach (var n in pending)
        {
            Console.WriteLine($"処理中: {n.VslCode}/{n.VoyNo}/{n.ContainerNo}");

            using var conn = DbHelper.GetConnection();

            // ③ コンテナを検索（キー：船＋Voy＋コンテナNo）
            long? containerId = null;
            using (var findCmd = conn.CreateCommand())
            {
                findCmd.CommandText = @"
SELECT Id
FROM T_CyContainer
WHERE VslCode = $Vsl
  AND VoyNo = $Voy
  AND ContainerNo = $Ctr;
";
                findCmd.Parameters.AddWithValue("$Vsl", n.VslCode);
                findCmd.Parameters.AddWithValue("$Voy", n.VoyNo);
                findCmd.Parameters.AddWithValue("$Ctr", n.ContainerNo);

                var result = findCmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    containerId = (long)result;
                }
            }

            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (containerId == null)
            {
                // 対応コンテナなし → エラー扱い
                Console.WriteLine("→ 対応コンテナなし (ERROR)");

                using var errCmd = conn.CreateCommand();
                errCmd.CommandText = @"
UPDATE T_NaxTantei
SET Status   = 'ERROR',
    UpdatedAt = $Now
WHERE Id = $Id;
";
                errCmd.Parameters.AddWithValue("$Now", now);
                errCmd.Parameters.AddWithValue("$Id", n.Id);
                errCmd.ExecuteNonQuery();

                continue;
            }

            // ④ コンテナを UPDATE
            var permitDateTime = n.PermitDate + " " + n.PermitTime;

            using (var updCy = conn.CreateCommand())
            {
                updCy.CommandText = @"
UPDATE T_CyContainer
SET PermitFlag     = 1,
    PermitDateTime = $PermitDateTime,
    LastNaccsMsg   = $Msg
WHERE Id = $Id;
";
                updCy.Parameters.AddWithValue("$PermitDateTime", permitDateTime);
                updCy.Parameters.AddWithValue("$Msg", n.MessageType);
                updCy.Parameters.AddWithValue("$Id", containerId.Value);
                updCy.ExecuteNonQuery();
            }

            // ⑤ 通知側のステータス更新（DONE）
            using (var updNt = conn.CreateCommand())
            {
                updNt.CommandText = @"
UPDATE T_NaxTantei
SET Status    = 'DONE',
    UpdatedAt = $Now
WHERE Id = $Id;
";
                updNt.Parameters.AddWithValue("$Now", now);
                updNt.Parameters.AddWithValue("$Id", n.Id);
                updNt.ExecuteNonQuery();
            }

            Console.WriteLine("→ 更新完了 (DONE)");
        }

        Console.WriteLine("=== FollowJob 終了 ===");
    }

    // ① 未処理通知を取得する関数
    static List<Notification> GetPendingNotifications()
    {
        var list = new List<Notification>();

        using var conn = DbHelper.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT Id, MessageType, VslCode, VoyNo, ContainerNo, PermitDate, PermitTime
FROM T_NaxTantei
WHERE Status = 'WAIT';
";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var n = new Notification
            {
                Id          = reader.GetInt64(0),
                MessageType = reader.GetString(1),
                VslCode     = reader.GetString(2),
                VoyNo       = reader.GetString(3),
                ContainerNo = reader.GetString(4),
                PermitDate  = reader.GetString(5),
                PermitTime  = reader.GetString(6)
            };
            list.Add(n);
        }

        return list;
    }
}
