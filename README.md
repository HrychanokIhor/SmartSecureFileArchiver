# SmartSecureFileArchiver

Навчальний архіватор SARC v1 на .NET 10. CLI створює захищені томи, відновлює файли незалежно від порядку томів і перевіряє CRC32 до запису. Стиснення блока: LZ77 та [канонічний Huffman](docs/SARC-V1-BLOCKS.md). Пакування томів використовує ДП-рюкзак; жадібний варіант доступний для порівняння.

Команди з кореня репозиторію:

```sh
dotnet build SmartArchiver.slnx
cli=src/SmartArchiver.CLI/bin/Debug/net10.0/SmartArchiver.CLI.dll
dotnet "$cli" train team results/model
dotnet "$cli" pack-set archive.arc 'test-password' team/team_config.json results/model team/corpus/f0000.txt team/corpus/f0001.py
dotnet "$cli" unpack-set restored 'test-password' archive.arc
dotnet "$cli" benchmark team results/model results/benchmark.json 'test-password'
python3 scripts/verify_sarc.py "$cli" team
dotnet test SmartArchiver.slnx
```

Для кількох томів `pack-set` виводить імена `archive.part0.arc`, `archive.part1.arc` тощо. Передай їх усі після пароля в `unpack-set` у будь-якому порядку. Каталог результату має бути новим. Замість каталогу моделі можна передати `-`: тоді використовується загальний LZ77 без навченого класифікатора. Для одного файла залишаються `pack <file> <volume> <password>` та `unpack <volume> <output-file> <password>`.

Конфігурація визначає розмір блока, ліміт запису тому, пріоритети та параметри метрики Q. Цей ліміт не обмежує розмір тому при читанні. Результати навчання й вимірювань дивись у [звіті T01](docs/T01-RESULTS.md). Історія ГА окремо показує найкращий результат за весь пошук і максимум кожного покоління. Час і Q можуть відрізнятися між запусками.

Поточна реалізація тримає файли й томи в пам’яті; сценарій для файлів більших за RAM ще не підтримується. `PREDICTION` і старий експериментальний HUF1 не є частиною файлового режиму SARC v1. Використання генерації коду задекларовано в [META-USAGE](META-USAGE).
