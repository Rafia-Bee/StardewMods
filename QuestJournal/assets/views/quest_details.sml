<frame layout="560px 480px"
       background={@Mods/StardewUI/Sprites/MenuBackground}
       border={@Mods/StardewUI/Sprites/MenuBorder}
       border-thickness="36, 36, 40, 36"
       padding="20, 16">
    <lane orientation="vertical" layout="stretch stretch">
        <banner layout="stretch content"
                margin="0, 0, 0, 16"
                background={@Mods/StardewUI/Sprites/BannerBackground}
                background-border-thickness="48, 0"
                padding="12"
                text={:Title} />
        <scrollable layout="stretch stretch"
                    scrollbar-visibility="visible"
                    scrollbar-margin="-44, 16, 0, 16"
                    scrollbar-track-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}
                    scrollbar-up-sprite={@Mods/RafiaBee.QuestJournal/Sprites/up_arrow:Arrow}
                    scrollbar-down-sprite={@Mods/RafiaBee.QuestJournal/Sprites/down_arrow:Arrow}
                    scrollbar-thumb-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}>
            <label margin="4, 0, 44, 0" text={:Description} />
        </scrollable>
    </lane>
</frame>
