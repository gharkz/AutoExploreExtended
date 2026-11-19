using System.Collections.Generic;

namespace AutoExploreExtended
{
    // UI text in the game's language: English is the key; Japanese and Chinese where we have them (English otherwise).
    // Format strings use {0}, {1}... and go through F().
    internal static class L
    {
        private static bool JP => Lang.langCode == "JP";
        private static bool CN => Lang.langCode == "CN" || Lang.langCode == "ZHTW";

        internal static string T(string en)
        {
            if (string.IsNullOrEmpty(en) || (!JP && !CN)) return en;
            return Table.TryGetValue(en, out var t) ? (JP ? t.jp : t.cn) ?? en : en;
        }

        internal static string F(string en, params object[] args) => string.Format(T(en), args);

        private static readonly Dictionary<string, (string jp, string cn)> Table = new Dictionary<string, (string, string)>
        {
            // Window, tabs, sections
            ["General"] = ("一般", "常规"),
            ["Toggles"] = ("切り替え", "开关"),
            ["Regen"] = ("回復", "恢复"),
            ["Performance"] = ("パフォーマンス", "性能"),
            ["Filters"] = ("フィルター", "过滤"),
            ["Tools"] = ("道具", "工具"),
            ["Debug"] = ("デバッグ", "调试"),
            ["Restrictions"] = ("除外リスト", "排除列表"),
            ["Materials"] = ("素材", "材质"),
            ["Locked containers"] = ("施錠された容器", "上锁的容器"),
            ["Digging"] = ("掘削", "挖掘"),
            ["Shearing"] = ("毛刈り", "剪毛"),
            ["Menu"] = ("メニュー", "菜单"),
            ["Compatibility"] = ("互換性", "兼容性"),

            // AutoExplore's settings (labels)
            ["Activation key"] = ("起動キー", "启动键"),
            ["Toggle harvesting and mining mode"] = ("採取・採掘モード切り替え", "切换采集与采矿模式"),
            ["Go down key"] = ("下り階段キー", "下楼键"),
            ["Go up key"] = ("上り階段キー", "上楼键"),
            ["Handle traps"] = ("罠を処理", "处理陷阱"),
            ["Use meditation"] = ("瞑想を使う", "使用冥想"),
            ["Handle fighting"] = ("戦闘を処理", "处理战斗"),
            ["Handle harvestables"] = ("採取物を処理", "处理可采集物"),
            ["Handle mineables"] = ("採掘物を処理", "处理可挖掘物"),
            ["Handle shrines"] = ("祠を処理", "处理神龛"),
            ["Stop at material shrine"] = ("素材の祠で止まる", "在材料神龛处停下"),
            ["Stop at armor shrine"] = ("防具の祠で止まる", "在护甲神龛处停下"),
            ["Handle statues"] = ("神像を処理", "处理神像"),
            ["Handle chests"] = ("宝箱を処理", "处理宝箱"),
            ["Auto descend"] = ("自動で降りる", "自动下楼"),
            ["Handle hunger"] = ("空腹を処理", "处理饥饿"),
            ["Allow on own land"] = ("自分の土地でも動作", "允许在自己的领地"),
            ["min MP"] = ("最低MP", "最低MP"),
            ["min HP"] = ("最低HP", "最低HP"),
            ["Log performance"] = ("性能をログに記録", "记录性能日志"),
            ["Use early exit flood"] = ("早期終了探索を使う", "使用提前结束搜索"),
            ["Gathering restriction list"] = ("採取除外リスト", "采集排除列表"),
            ["Mining restriction list"] = ("採掘除外リスト", "采矿排除列表"),
            ["Pickup restriction list"] = ("拾得除外リスト", "拾取排除列表"),

            // Our settings (labels)
            ["Lockpicks ignore instant actions"] = ("ロックピックはInstant Actionsを無視", "开锁器忽略Instant Actions"),
            ["Crowbars ignore instant actions"] = ("バールはInstant Actionsを無視", "撬棍忽略Instant Actions"),
            ["Practice chests (no damage, no charges used)"] = ("練習用の箱（壊さず、回数も消費しない）", "练习箱（不损坏、不消耗次数）"),
            ["Include karma containers (NPC or lost property)"] = ("カルマが下がる容器も開ける（NPCの物・落とし物）", "包括会降低业力的容器（NPC财物或失物）"),
            ["Open locked containers (crowbar or lockpick)"] = ("施錠された容器を開ける（バールかロックピック）", "打开上锁的容器（撬棍或开锁器）"),
            ["Dig floors and tracks (when shovel tool selected)"] = ("床と線路を掘る（シャベル選択時）", "挖掘地板和轨道（选中铲子时）"),
            ["Shear animals (when shears tool selected)"] = ("動物の毛を刈る（ハサミ選択時）", "给动物剪毛（选中剪刀时）"),
            ["Dig tracks while mining (swaps to your best shovel)"] = ("採掘中に線路も掘る（最良のシャベルに持ち替え）", "采矿时也挖轨道（换用最好的铲子）"),
            ["Show in your own action menu (middle-click yourself)"] = ("自分の行動メニューに表示（自分を中クリック）", "在自己的行动菜单中显示（中键点击自己）"),
            ["Shear animals (off: the Shearing Extension handles it)"] = ("動物の毛を刈る（オフ：Shearing Extensionが担当）", "给动物剪毛（关闭：由Shearing Extension负责）"),
            ["Profile"] = ("計測", "性能分析"),

            // Our settings (descriptions, shown as hover hints)
            ["Open locked containers you may legally open, with AutoExplore in Gather/Combined mode while a crowbar or lockpick is selected."] =
                ("バールかロックピックを選んだ状態で、採取／複合モードのAutoExploreが、合法に開けられる施錠された容器を開けます。",
                 "选中撬棍或开锁器时，采集/综合模式的AutoExplore会打开可以合法打开的上锁容器。"),
            ["Shear every shearable NPC with AutoExplore in Gather/Combined mode while shears are selected."] =
                ("ハサミを選んだ状態で、採取／複合モードのAutoExploreが毛を刈れるNPCをすべて刈ります。",
                 "选中剪刀时，采集/综合模式的AutoExplore会给所有可剪毛的NPC剪毛。"),
            ["Also train on locked practice chests while holding a lockpick or crowbar. They are always picked by hand: never pried, never damaged, and no lockpick charges are used."] =
                ("ロックピックかバールを持っているとき、施錠された練習用の箱でも訓練します。必ず手で解錠し、こじ開けたり壊したりせず、ロックピックの回数も消費しません。",
                 "持有开锁器或撬棍时，也会在上锁的练习箱上训练。始终徒手开锁：不撬、不损坏，也不消耗开锁器次数。"),
            ["Locked containers only: also open ones that cost karma or count as a crime (NPC property, lost property). Gathering and mining are not affected."] =
                ("施錠された容器のみ：カルマが下がる、または犯罪になる容器（NPCの物、落とし物）も開けます。採取と採掘には影響しません。",
                 "仅限上锁的容器：也打开会降低业力或算作犯罪的容器（NPC财物、失物）。不影响采集和采矿。"),
            ["Prying (crowbar) by this mod runs at its normal length even with the Instant Actions mod on; instant prying stops after one tick and never opens anything."] =
                ("Instant Actions MODが有効でも、このMODのこじ開け（バール）は通常の長さで行います。即時のこじ開けは1回で止まり、何も開きません。",
                 "即使启用了Instant Actions MOD，本MOD的撬开（撬棍）也按正常时长进行；即时撬开只进行一次就停止，打不开任何东西。"),
            ["Picking (lockpick) by this mod runs at its normal length even with the Instant Actions mod on."] =
                ("Instant Actions MODが有効でも、このMODの解錠（ロックピック）は通常の長さで行います。",
                 "即使启用了Instant Actions MOD，本MOD的开锁（开锁器）也按正常时长进行。"),
            ["Dig up floors with AutoExplore in Gather, Mining or Combined mode while a shovel is selected."] =
                ("シャベルを選んだ状態で、採取／採掘／複合モードのAutoExploreが床を掘ります。",
                 "选中铲子时，采集/采矿/综合模式的AutoExplore会挖起地板。"),
            ["While mining with AutoExplore, also dig up tracks using your hardest carried shovel (swapped in and back automatically)."] =
                ("AutoExploreで採掘中、持っている最も硬いシャベルで線路も掘ります（自動で持ち替えて戻します）。",
                 "用AutoExplore采矿时，也用携带的最硬的铲子挖起轨道（自动换上并换回）。"),
            ["Show \"AutoExplore Extended\" in your action menu (the game's \"All Action\", middle-click by default; rebind it under Options > Input) when used on yourself. System > Mod always has it."] =
                ("自分に対する行動メニュー（ゲームの「全ての行動」、初期設定は中クリック。オプション＞入力で変更可）に「AutoExplore Extended」を表示します。システム＞MODからは常に開けます。",
                 "对自己使用行动菜单（游戏的“所有行动”，默认中键；可在选项>输入中更改）时显示“AutoExplore Extended”。系统>MOD中始终可以打开。"),
            ["Time AutoExplore's search while it runs; summary every 5 s in the BepInEx log and autoexplore-profile.txt."] =
                ("AutoExploreの探索処理を計測し、5秒ごとにBepInExのログとautoexplore-profile.txtにまとめを書き出します。",
                 "在AutoExplore运行时计时其搜索，每5秒将摘要写入BepInEx日志和autoexplore-profile.txt。"),
            ["Pickaxe work (walls, veins, rocks): Off, Blacklist (skip the active blacklist) or Whitelist (only the active whitelist)."] =
                ("つるはしの作業（壁・鉱脈・岩）：オフ、ブラックリスト（有効なリストを除外）、ホワイトリスト（有効なリストのみ）。",
                 "镐的工作（墙壁、矿脉、岩石）：关闭、黑名单（跳过当前黑名单）或白名单（只处理当前白名单）。"),
            ["Axe work (trees, wood): Off, Blacklist or Whitelist."] = ("斧の作業（木）：オフ、ブラックリスト、ホワイトリスト。", "斧的工作（树木、木材）：关闭、黑名单或白名单。"),
            ["Shovel work (floors, tracks): Off, Blacklist or Whitelist."] = ("シャベルの作業（床・線路）：オフ、ブラックリスト、ホワイトリスト。", "铲子的工作（地板、轨道）：关闭、黑名单或白名单。"),

            // Window notes
            ["This mod's settings."] = ("このMODの設定です。", "本MOD的设置。"),
            ["AutoExplore's settings save to AutoExplore's config file; this mod's settings save to its own."] =
                ("AutoExploreの設定はAutoExploreの設定ファイルに、このMODの設定はこのMOD自身のファイルに保存されます。",
                 "AutoExplore的设置保存到AutoExplore的配置文件；本MOD的设置保存到它自己的文件。"),
            ["AutoExplore's own settings, saved to its config file."] = ("AutoExplore自身の設定です。その設定ファイルに保存されます。", "AutoExplore自己的设置，保存到它的配置文件。"),
            ["Point at a setting to see what it does."] = ("設定にマウスを合わせると説明が表示されます。", "将鼠标指向某项设置即可查看说明。"),
            ["Changes take effect immediately. No need to restart the game."] = ("変更はすぐに反映されます。ゲームの再起動は不要です。", "更改立即生效，无需重启游戏。"),
            ["Reset sliders to defaults ({0})"] = ("スライダーを初期値に戻す（{0}）", "将滑块恢复默认值（{0}）"),
            ["(none)"] = ("（なし）", "（无）"),
            ["press a key…"] = ("キーを押してください…", "请按一个键…"),

            // Materials
            ["AutoExplore skips targets whose material does not pass the selected tool's filter."] =
                ("選んだ道具のフィルターを通らない素材の対象は、AutoExploreがスキップします。", "材质未通过所选工具过滤条件的目标，AutoExplore会跳过。"),
            ["Blacklist skips the listed materials; Whitelist takes only them."] =
                ("ブラックリストは載っている素材を除外し、ホワイトリストは載っている素材だけを対象にします。", "黑名单跳过列出的材质；白名单只处理列出的材质。"),
            ["Pickaxe"] = ("つるはし", "镐"),
            ["Axe"] = ("斧", "斧"),
            ["Shovel"] = ("シャベル", "铲子"),
            [" (walls, veins, rocks)"] = ("（壁・鉱脈・岩）", "（墙壁、矿脉、岩石）"),
            [" (trees, wood)"] = ("（木）", "（树木、木材）"),
            [" (floors, tracks)"] = ("（床・線路）", "（地板、轨道）"),
            ["Filter"] = ("フィルター", "过滤"),
            ["Off"] = ("オフ", "关闭"),
            ["Blacklist"] = ("ブラックリスト", "黑名单"),
            ["Whitelist"] = ("ホワイトリスト", "白名单"),
            ["{0} filter"] = ("{0}のフィルター", "{0}过滤"),
            ["{0} blacklists"] = ("{0}のブラックリスト一覧", "{0}黑名单一览"),
            ["{0} whitelists"] = ("{0}のホワイトリスト一覧", "{0}白名单一览"),
            ["{0} materials"] = ("{0}種の素材", "{0}种材质"),
            ["Whitelist: only these materials are taken. Comma separated; names (birch) or ids (wood_birch)."] =
                ("ホワイトリスト：これらの素材だけを対象にします。カンマ区切り。名前（birch）またはID（wood_birch）。", "白名单：只处理这些材质。用逗号分隔；名称（birch）或ID（wood_birch）。"),
            ["Blacklist: these materials are skipped. Comma separated; names (birch) or ids (wood_birch)."] =
                ("ブラックリスト：これらの素材をスキップします。カンマ区切り。名前（birch）またはID（wood_birch）。", "黑名单：跳过这些材质。用逗号分隔；名称（birch）或ID（wood_birch）。"),
            ["Click a list to make it active.\nYou can't delete the last list; keep at least one of each kind.\nName lists for what they do, e.g. {0}."] =
                ("リストをクリックすると有効になります。\n最後のリストは削除できません。種類ごとに最低1つ必要です。\nリストには内容が分かる名前を付けてください（例：{0}）。",
                 "点击列表即可启用。\n无法删除最后一个列表；每种至少保留一个。\n请按用途给列表命名，例如“{0}”。"),
            ["Unknown material: {0}. Pick from the checklist."] = ("不明な素材：{0}。チェックリストから選んでください。", "未知材质：{0}。请从勾选列表中选择。"),
            ["Woods"] = ("木材", "木材"),
            ["Metals and ores"] = ("金属と鉱石", "金属和矿石"),
            ["Stones"] = ("石材", "石材"),
            ["Gems"] = ("宝石", "宝石"),
            ["Crystals"] = ("結晶", "晶体"),
            ["Soils"] = ("土", "土壤"),

            // Lists windows
            ["List"] = ("リスト", "列表"),
            ["Entries"] = ("項目", "条目"),
            ["(empty)"] = ("（空）", "（空）"),
            ["{0} entries"] = ("{0}件", "{0}项"),
            ["Edit"] = ("編集", "编辑"),
            ["Rename"] = ("名前変更", "重命名"),
            ["Delete"] = ("削除", "删除"),
            ["New List"] = ("新しいリスト", "新建列表"),
            ["New list"] = ("新しいリスト", "新建列表"),
            ["Rename {0}"] = ("{0}の名前を変更", "重命名 {0}"),
            ["Delete the list \"{0}\"?"] = ("リスト「{0}」を削除しますか？", "删除列表“{0}”？"),
            ["That name is empty, too long or already used."] = ("その名前は空か、長すぎるか、すでに使われています。", "该名称为空、过长或已被使用。"),
            ["{0} lists"] = ("{0}の一覧", "{0}一览"),
            ["Click a list to make it active. AutoExplore uses the active one.\nYou can't delete the last list; keep at least one.\nName lists for what they do, e.g. no_moss_or_pebbles."] =
                ("リストをクリックすると有効になります。AutoExploreは有効なリストを使います。\n最後のリストは削除できません。最低1つ必要です。\nリストには内容が分かる名前を付けてください（例：苔と小石を除外）。",
                 "点击列表即可启用。AutoExplore使用当前启用的列表。\n无法删除最后一个列表；至少保留一个。\n请按用途给列表命名，例如“排除苔藓和石子”。"),

            // Example list names (shown in the lists windows' notes)
            ["cedar_and_birch_only"] = ("杉と白樺のみ", "仅雪松和白桦"),
            ["no_rosewood_and_oak"] = ("紫檀と樫を除外", "排除紫檀和橡木"),
            ["mithril_and_adamantite_only"] = ("ミスリルとアダマンタイトのみ", "仅秘银和精金"),
            ["no_granite_or_marble"] = ("花崗岩と大理石を除外", "排除花岗岩和大理石"),
            ["soil_only"] = ("土のみ", "仅土壤"),
            ["no_grass_or_sand"] = ("草と砂を除外", "排除草和沙子"),

            // Text editor and checklist
            ["OK"] = ("OK", "确定"),
            ["Cancel"] = ("キャンセル", "取消"),
            ["Raw list, as saved in the config file. Type here or select items on the right."] =
                ("設定ファイルに保存されるそのままのリストです。ここに入力するか、右側で項目を選んでください。", "保存在配置文件中的原始列表。可在此输入，或在右侧选择项目。"),
            ["In the list"] = ("リストに含まれるもの", "已在列表中"),
            ["Common to skip"] = ("よく除外するもの", "常见排除项"),
            ["Everything else"] = ("その他すべて", "其他全部"),
            ["Not allowed: {0}. Use names separated by commas."] = ("使えない文字：{0}。名前をカンマで区切ってください。", "不允许：{0}。请用逗号分隔名称。"),
            ["Not seen here (kept anyway): {0}"] = ("この場所には見当たりません（そのまま保持）：{0}", "此处未见（仍保留）：{0}"),
        };
    }
}
