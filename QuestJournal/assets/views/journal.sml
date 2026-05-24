<lane orientation="vertical"
      horizontal-content-alignment="middle"
      vertical-content-alignment="middle">
    <frame layout="800px 600px"
           background={@Mods/StardewUI/Sprites/MenuBackground}
           border={@Mods/StardewUI/Sprites/MenuBorder}
           border-thickness="36, 36, 40, 36"
           padding="24, 16">
        <lane orientation="vertical" horizontal-content-alignment="middle">
            <banner layout="500px content"
                    margin="0, 0, 0, 8"
                    background={@Mods/StardewUI/Sprites/BannerBackground}
                    background-border-thickness="48, 0"
                    padding="12"
                    text={:Title} />
            <label margin="0, 8, 0, 16" text={:Summary} />
            <lane orientation="vertical"
                  layout="stretch content"
                  horizontal-content-alignment="start">
                <lane *repeat={:Quests}
                      orientation="vertical"
                      layout="stretch content"
                      margin="0, 4">
                    <label color="#136" text={:Title} />
                    <label margin="16, 2, 0, 0" text={:Objective} />
                </lane>
            </lane>
        </lane>
    </frame>
</lane>
