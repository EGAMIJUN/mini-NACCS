using System;
using System.IO;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== mini-NACCS 電文生成ツール ===");

        // 1. 出力情報コード（とりあえず YO 固定でもOK）
        Console.Write("出力情報コード（例: YO）[default: YO]: ");
        var msgType = ReadOrDefault("YO");

        // 2. 船コード
        Console.Write("船コード (VslCode) [例: VSL001]: ");
        var vsl = ReadRequired("船コード");

        // 3. Voy
        Console.Write("VoyNo [例: V001]: ");
        var voy = ReadRequired("VoyNo");

        // 4. コンテナ本数
        Console.Write("コンテナ本数を入力してください [例: 2]: ");
        int count = ReadInt("コンテナ本数");

        // 5. 許可日
        Console.Write("許可日 (YYYYMMDD) 空Enterで今日: ");
        var permitDate = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(permitDate))
        {
            permitDate = DateTime.Now.ToString("yyyyMMdd");
        }

        // 6. 許可時間
        Console.Write("許可時刻 (HHmmss) 空Enterで現在時刻: ");
        var permitTime = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(permitTime))
        {
            permitTime = DateTime.Now.ToString("HHmmss");
        }

        // 7. コンテナNoを複数入力
        var containers = new List<string>();
        for (int i = 0; i < count; i++)
        {
            Console.Write($"[{i + 1}/{count}] コンテナNo を入力してください: ");
            var cno = ReadRequired("コンテナNo");
            containers.Add(cno);
        }

        // 8. 電文テキストを組み立てる
        var lines = new List<string>();

        // HDR 行
        lines.Add($"HDR,{msgType},{permitDate}");

        // DTL 行（コンテナ本数分）
        foreach (var cno in containers)
        {
            // DTL,出力情報コード,Vsl,Voy,コンテナNo,許可日,許可時刻
            lines.Add($"DTL,{msgType},{vsl},{voy},{cno},{permitDate},{permitTime}");
        }

        var text = string.Join(Environment.NewLine, lines);

        // 9. ファイルパスを決定（ソリューションルート/NaccsInbox）
        var baseDir = Directory.GetParent(Environment.CurrentDirectory)!.FullName;
        var inboxDir = Path.Combine(baseDir, "NaccsInbox");
        Directory.CreateDirectory(inboxDir);

        var fileName = $"{msgType}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var filePath = Path.Combine(inboxDir, fileName);

        // 10. ファイルに書き込む
        File.WriteAllText(filePath, text);

        Console.WriteLine();
        Console.WriteLine("=== 電文ファイルを出力しました ===");
        Console.WriteLine($"ファイル: {filePath}");
        Console.WriteLine("--- 内容 ---");
        Console.WriteLine(text);
        Console.WriteLine("============");
    }

    // 入力が空ならデフォルト値を使う
    static string ReadOrDefault(string defaultValue)
    {
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }
        return input.Trim();
    }

    // 必須入力（空だと再入力させる）
    static string ReadRequired(string label)
    {
        while (true)
        {
            var input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
            {
                return input.Trim();
            }
            Console.Write($"{label} は必須です。もう一度入力してください: ");
        }
    }

    // 整数入力
    static int ReadInt(string label)
    {
        while (true)
        {
            var input = Console.ReadLine();
            if (int.TryParse(input, out var value) && value > 0)
            {
                return value;
            }
            Console.Write($"{label} は 1以上の数値で入力してください: ");
        }
    }
}
