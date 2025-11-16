#!/bin/zsh

cd /Users/egamijun/mini-naccs

while true
do
  echo "=== ジョブ実行開始 ==="
  date

  # 受信JOB
  dotnet run --project NxReceive/NxReceive.csproj

  # 後続JOB
  dotnet run --project FollowJob/FollowJob.csproj

  echo "=== 60秒スリープ ==="
  sleep 10
done
