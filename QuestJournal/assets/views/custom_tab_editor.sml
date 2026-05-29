<frame layout="540px content"
       background={@Mods/StardewUI/Sprites/MenuBackground}
       border={@Mods/StardewUI/Sprites/MenuBorder}
       border-thickness="36, 36, 40, 36"
       padding="32, 24"
       horizontal-content-alignment="middle">
    <lane orientation="vertical" layout="stretch content" horizontal-content-alignment="middle">
        <label layout="stretch content" bold="true" horizontal-alignment="middle" text={HeaderText} />
        <label layout="stretch content" margin="0, 12, 0, 8" text="This tab will list quests matching the filters below. Leave a filter blank to ignore it." />

        <label layout="stretch content" margin="0, 12, 0, 4" text="Tab name" />
        <textinput layout="stretch 64px"
                   max-length="30"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>Name} />

        <label layout="stretch content" margin="0, 12, 0, 4" text="Title contains" />
        <textinput layout="stretch 64px"
                   max-length="40"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>TitleFilter} />

        <label layout="stretch content" margin="0, 12, 0, 4" text="Source contains" />
        <textinput layout="stretch 64px"
                   max-length="40"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>SourceFilter} />

        <label layout="stretch content" margin="0, 12, 0, 4" text="Category contains" />
        <textinput layout="stretch 64px"
                   max-length="40"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>CategoryFilter} />
        <label *if={HasCategoryHint} layout="stretch content" margin="0, 4, 0, 0" text={CategoryHint} />

        <label layout="stretch content" margin="0, 12, 0, 4" text="Kind contains" />
        <textinput layout="stretch 64px"
                   max-length="40"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>KindFilter} />
        <label *if={HasKindHint} layout="stretch content" margin="0, 4, 0, 0" text={KindHint} />

        <lane orientation="horizontal" margin="0, 24, 0, 0" horizontal-content-alignment="middle">
            <frame margin="0, 0, 12, 0"
                   padding="20, 12"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   click=|Save()|>
                <label text="Save" />
            </frame>
            <frame *if={ShowDelete}
                   margin="0, 0, 12, 0"
                   padding="20, 12"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   click=|Delete()|>
                <label text="Delete" />
            </frame>
            <frame padding="20, 12"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   click=|Cancel()|>
                <label text="Cancel" />
            </frame>
        </lane>
    </lane>
</frame>
