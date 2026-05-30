<frame layout="420px content"
       background={@Mods/StardewUI/Sprites/MenuBackground}
       border={@Mods/StardewUI/Sprites/MenuBorder}
       border-thickness="36, 36, 40, 36"
       padding="28, 20"
       horizontal-content-alignment="middle">
    <lane orientation="vertical" layout="stretch content" horizontal-content-alignment="middle">
        <label layout="stretch content" bold="true" horizontal-alignment="middle" margin="0, 0, 0, 12" text={:Title} />
        <lane *repeat={:Options} orientation="vertical" layout="stretch content">
            <frame layout="stretch content"
                   margin="0, 4"
                   padding="16, 12"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   click=|Choose()|>
                <label text={:Label} />
            </frame>
        </lane>
    </lane>
</frame>
