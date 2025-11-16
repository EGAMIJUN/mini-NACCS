using Microsoft.Data.Sqlite;
using System;
using System.IO;

public static class DbHelper
{
    // DBファイルの場所（今回は固定でハードコーディングしちゃう）
    // ※ mini-naccs フォルダ直下に miniportit.db を作る
    private const string DbFilePath = "/Users/egamijun/mini-naccs/miniportit.db";

    // DB接続を作る
    public static SqliteConnection GetConnection()
    {
        // フォルダがなければ作る（念のため）
        var dir = Path.GetDirectoryName(DbFilePath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir!);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DbFilePath
        };

        var conn = new SqliteConnection(builder.ToString());
        conn.Open();
        return conn;
    }

    // 必要なテーブルがなければ作る
    public static void EnsureSchema()
    {
        using var conn = GetConnection();

        // 通知テーブル（SXGW側）
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS T_NaxTantei (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    MessageType  TEXT,
    VslCode      TEXT,
    VoyNo        TEXT,
    ContainerNo  TEXT,
    PermitDate   TEXT,
    PermitTime   TEXT,
    Status       TEXT,
    CreatedAt    TEXT,
    UpdatedAt    TEXT
);";
            cmd.ExecuteNonQuery();
        }

        // コンテナ本体テーブル（PORT-IT 側）
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS T_CyContainer (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    VslCode        TEXT,
    VoyNo          TEXT,
    ContainerNo    TEXT,
    InternalNo     TEXT,
    PermitFlag     INTEGER,
    PermitDateTime TEXT,
    LastNaccsMsg   TEXT
);";
            cmd.ExecuteNonQuery();
        }
    }

    // テスト用にコンテナ1件だけ入れておく（後続JOBのときに使う）
    public static void SeedSampleContainer()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO T_CyContainer (VslCode, VoyNo, ContainerNo, InternalNo, PermitFlag)
SELECT 'VSL001','V001','CNT001','INT-0001',0
WHERE NOT EXISTS (
    SELECT 1 FROM T_CyContainer
    WHERE VslCode = 'VSL001'
      AND VoyNo = 'V001'
      AND ContainerNo = 'CNT001'
);";
        cmd.ExecuteNonQuery();
    }
}
