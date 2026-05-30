<frame layout="540px content"
       background={@Mods/StardewUI/Sprites/MenuBackground}
       border={@Mods/StardewUI/Sprites/MenuBorder}
       border-thickness="36, 36, 40, 36"
       padding="32, 24"
       horizontal-content-alignment="middle">
    <lane orientation="vertical" layout="stretch content" horizontal-content-alignment="middle">
        <label layout="stretch content" bold="true" horizontal-alignment="middle" text={:HeaderText} />
        <label layout="stretch content" margin="0, 12, 0, 8" text={#tabeditor.intro} />

        <label layout="stretch content" margin="0, 12, 0, 4" text={#tabeditor.name} />
        <textinput layout="stretch 64px"
                   max-length="30"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>Name} />

        <label layout="stretch content" margin="0, 12, 0, 4" text={#tabeditor.title} />
        <textinput layout="stretch 64px"
                   max-length="40"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>TitleFilter} />

        <label layout="stretch content" margin="0, 12, 0, 4" text={#tabeditor.source} />
        <textinput layout="stretch 64px"
                   max-length="40"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>SourceFilter} />

        <label layout="stretch content" margin="0, 12, 0, 4" text={#tabeditor.category} />
        <textinput layout="stretch 64px"
                   max-length="40"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>CategoryFilter} />
        <label *if={:HasCategoryHint} layout="stretch content" margin="0, 4, 0, 0" text={:CategoryHint} />

        <label layout="stretch content" margin="0, 12, 0, 4" text={#tabeditor.kind} />
        <textinput layout="stretch 64px"
                   max-length="40"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>KindFilter} />
        <label *if={:HasKindHint} layout="stretch content" margin="0, 4, 0, 0" text={:KindHint} />

        <lane orientation="horizontal" margin="0, 24, 0, 0" horizontal-content-alignment="middle">
            <frame margin="0, 0, 12, 0"
                   padding="20, 12"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   click=|Save()|>
                <label text={#tabeditor.save} />
            </frame>
            <frame *if={:ShowDelete}
                   margin="0, 0, 12, 0"
                   padding="20, 12"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   click=|Delete()|>
                <label text={#tabeditor.delete} />
            </frame>
            <frame padding="20, 12"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   click=|Cancel()|>
                <label text={#tabeditor.cancel} />
            </frame>
        </lane>
    </lane>
</frame>
