<lane orientation="vertical"
      horizontal-content-alignment="middle"
      vertical-content-alignment="middle">

    <frame layout={RootLayout}
           background={@Mods/StardewUI/Sprites/MenuBackground}
           border={@Mods/StardewUI/Sprites/MenuBorder}
           border-thickness="36, 36, 40, 36"
           padding="20, 12"
           horizontal-content-alignment="middle">

        <lane *float="above"
              orientation="vertical"
              horizontal-content-alignment="start"
              margin="36, 0, 0, -8">
            <lane *repeat={:OverflowRowGroups} orientation="horizontal" vertical-content-alignment="end">
                <frame *repeat={:Tabs}
                       layout={WidthLayout}
                       margin="0, 0, 8, 0"
                       padding="8, 0"
                       background={@Mods/StardewUI/Sprites/ControlBorder}
                       horizontal-content-alignment="middle"
                       vertical-content-alignment="middle"
                       focusable="true"
                       opacity={TabOpacity}
                       tooltip={Label}
                       click=|Activate()|>
                    <label bold={IsActive} text={DisplayLabel} />
                </frame>
            </lane>

            <lane orientation="horizontal" vertical-content-alignment="end">
                <frame *repeat={:BottomRowTabs}
                       layout={WidthLayout}
                       margin="0, 0, 8, 0"
                       padding="8, 0"
                       background={@Mods/StardewUI/Sprites/ControlBorder}
                       horizontal-content-alignment="middle"
                       vertical-content-alignment="middle"
                       focusable="true"
                       opacity={TabOpacity}
                       tooltip={Label}
                       click=|Activate()|>
                    <label bold={IsActive} text={DisplayLabel} />
                </frame>
            </lane>
        </lane>

        <lane *float="above"
              orientation="horizontal"
              vertical-content-alignment="end"
              margin={ControlsLeftMargin}>
            <frame layout={AddTab.WidthLayout}
                   margin="0, 0, 8, 0"
                   padding="8, 0"
                   background={@Mods/StardewUI/Sprites/ControlBorder}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   opacity={AddTab.TabOpacity}
                   tooltip={AddTab.Label}
                   click=|CreateTab()|>
                <image layout="32px 32px" fit="Contain" sprite={@Mods/StardewUI/Sprites/SmallGreenPlus} />
            </frame>

            <frame layout={EditTab.WidthLayout}
                   padding="8, 0"
                   background={@Mods/StardewUI/Sprites/ControlBorder}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   opacity={EditTab.TabOpacity}
                   tooltip={EditTab.Label}
                   click=|ToggleEditMode()|>
                <image layout="38px 38px" fit="Contain" sprite={@Mods/RafiaBee.QuestJournal/Sprites/edit:Edit} />
            </frame>
        </lane>

        <lane orientation="vertical" layout="stretch stretch" horizontal-content-alignment="middle">

            <lane orientation="horizontal" layout="stretch content" margin="0, 0, 0, 8" vertical-content-alignment="middle">
                <label margin="0, 0, 8, 0" text={#journal.sort.label} />
                <dropdown layout="220px content"
                          options={:SortOptions}
                          selected-option={<>SelectedSortLabel} />
                <frame layout="stretch content" />
                <label margin="0, 0, 8, 0" text={#journal.search.label} />
                <textinput layout="320px 56px"
                           max-length="40"
                           background={@Mods/StardewUI/Sprites/TextBox}
                           text={<>SearchText} />
                <frame margin="8, 0, 0, 0"
                       padding="10, 6"
                       background={@Mods/StardewUI/Sprites/ButtonLight}
                       horizontal-content-alignment="middle"
                       vertical-content-alignment="middle"
                       focusable="true"
                       tooltip={#journal.search.clear}
                       click=|ClearSearch()|>
                    <label text={#journal.search.clearlabel} />
                </frame>
            </lane>

            <label *if={EditMode}
                   margin="0, 0, 0, 8"
                   text={#journal.editmode.hint} />

            <lane orientation="horizontal" layout={PanelRowLayout} vertical-content-alignment="start">

                <frame layout={ListPanelLayout}
                       padding="20"
                       background={@Mods/StardewUI/Sprites/ControlBorder}>
                    <scrollable layout="stretch stretch"
                                scrollbar-visibility="visible"
                                scrollbar-margin="-44, 16, 0, 16"
                                scrollbar-track-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}
                                scrollbar-up-sprite={@Mods/RafiaBee.QuestJournal/Sprites/up_arrow:Arrow}
                                scrollbar-down-sprite={@Mods/RafiaBee.QuestJournal/Sprites/down_arrow:Arrow}
                                scrollbar-thumb-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}>
                        <lane orientation="vertical" layout="stretch content" margin="0, 0, 40, 0">
                            <lane *repeat={:Quests} orientation="vertical" layout="stretch content">
                                <frame layout="stretch content"
                                       padding="10, 8"
                                       background={@Mods/RafiaBee.QuestJournal/Sprites/highlight:Highlight}
                                       background-tint={RowTint}
                                       focusable="true"
                                       click=|Select()|
                                       pointer-enter=|HoverEnter()|
                                       pointer-leave=|HoverLeave()|>
                                    <lane orientation="horizontal" layout="stretch content" vertical-content-alignment="middle">
                                        <image *if={ReadyToClaim} layout="22px 22px" margin="0, 0, 6, 0" fit="Contain" sprite={@Mods/RafiaBee.QuestJournal/Sprites/coin:Coin} tooltip={#journal.list.claimtag} />
                                        <image *if={IsPinned} layout="18px 20px" margin="0, 0, 6, 0" fit="Contain" sprite={@Mods/RafiaBee.QuestJournal/Sprites/pin:Pin} tooltip={#journal.list.pinnedtag} />
                                        <label bold={IsSelected} text={:Title} />
                                    </lane>
                                </frame>
                                <frame *if={ShowDivider}
                                       layout="stretch 3px"
                                       margin="6, 4"
                                       background={@Mods/RafiaBee.QuestJournal/Sprites/highlight:Highlight}
                                       background-tint={DividerTint} />
                            </lane>
                            <label *if={IsEmpty} margin="8" text={#journal.list.empty} />
                        </lane>
                    </scrollable>
                </frame>

                <frame layout={DetailPanelLayout}
                       margin="8, 0"
                       padding="20"
                       background={@Mods/StardewUI/Sprites/ControlBorder}>
                    <scrollable layout="stretch stretch"
                                scrollbar-visibility="visible"
                                scrollbar-margin="-44, 16, 0, 16"
                                scrollbar-track-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}
                                scrollbar-up-sprite={@Mods/RafiaBee.QuestJournal/Sprites/up_arrow:Arrow}
                                scrollbar-down-sprite={@Mods/RafiaBee.QuestJournal/Sprites/down_arrow:Arrow}
                                scrollbar-thumb-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}>
                        <lane orientation="vertical" layout="stretch content" margin="0, 0, 40, 0">
                            <label *if={NoSelection} margin="8, 16" text={#journal.detail.empty} />
                            <lane *if={HasSelection} orientation="vertical" layout="stretch content">
                            <frame layout="stretch content"
                                   margin="0, 0, 0, 12"
                                   background={@Mods/StardewUI/Sprites/BannerBackground}
                                   border-thickness="48, 12"
                                   padding="0, 4"
                                   horizontal-content-alignment="middle"
                                   vertical-content-alignment="middle"
                                   focusable="true">
                                <label layout="stretch content"
                                       bold="true"
                                       horizontal-alignment="middle"
                                       max-lines="2"
                                       text={SelectedTitle} />
                            </frame>
                            <label margin="0, 4" focusable="true" text={SelectedDescription} />
                            <label *if={SelectedShowObjective} margin="0, 12, 0, 4" focusable="true" color={HeaderColor} text={#journal.header.objective} />
                            <label *if={SelectedShowObjective} margin="8, 0" text={SelectedObjective} />
                            <label *if={SelectedHasSteps} margin="0, 12, 0, 4" focusable="true" color={HeaderColor} text={#journal.header.steps} />
                            <label *repeat={:SelectedSteps} margin="8, 1" text={:RowText} />
                            <label *if={SelectedShowRewards} margin="0, 12, 0, 4" focusable="true" color={HeaderColor} text={#journal.header.rewards} />
                            <lane *repeat={:SelectedRewards} orientation="horizontal" margin="8, 1" vertical-content-alignment="middle">
                                <label text="- " />
                                <label text={:Summary} />
                            </lane>
                            <label *if={SelectedShowGiver} margin="0, 12, 0, 4" focusable="true" color={HeaderColor} text={#journal.header.giver} />
                            <label *if={SelectedShowGiver} margin="8, 0" text={SelectedGiverDisplay} />
                            <label margin="0, 12, 0, 4" focusable="true" color={HeaderColor} text={#journal.header.daysleft} />
                            <label margin="8, 0" text={SelectedDaysLeftDisplay} />
                            <label margin="0, 12, 0, 4" focusable="true" color={HeaderColor} text={#journal.header.source} />
                            <label margin="8, 0" text={SelectedSourceDisplay} />
                            </lane>
                        </lane>
                    </scrollable>
                </frame>

                <frame layout={ActionPanelLayout}
                       padding="20"
                       background={@Mods/StardewUI/Sprites/ControlBorder}>
                    <scrollable layout="stretch stretch"
                                scrollbar-visibility="hidden"
                                scrollbar-track-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}
                                scrollbar-up-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}
                                scrollbar-down-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}
                                scrollbar-thumb-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}>
                        <lane orientation="vertical" layout={ActionLaneLayout} horizontal-content-alignment="middle">
                        <label *if={SelectedIsCompleted} margin="0, 12" color={HeaderColor} text={#journal.status.questcomplete} />

                        <frame *if={SelectedCanClaim} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|ClaimSelected()|>
                            <label text={#journal.action.claim} />
                        </frame>
                        <frame *if={SelectedShowExternalComplete} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|ExternalCompleteSelected()|>
                            <label text={#journal.action.markdone} />
                        </frame>
                        <frame *if={SelectedShowExternalCancel} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|ExternalCancelSelected()|>
                            <label text={#journal.action.remove} />
                        </frame>
                        <frame *if={SelectedShowDetails} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|ShowDetailsSelected()|>
                            <label text={#journal.action.details} />
                        </frame>
                        <frame *if={SelectedShowPin} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|PinSelected()|>
                            <label text={SelectedPinLabel} />
                        </frame>
                        <frame *if={SelectedShowComplete} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|CompleteSelected()|>
                            <label text={#journal.action.complete} />
                        </frame>
                        <frame *if={SelectedShowCompleteOrder} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|CompleteOrderSelected()|>
                            <label text={#journal.action.complete} />
                        </frame>
                        <frame *if={SelectedShowWarp} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|WarpSelected()|>
                            <label text={SelectedWarpLabel} />
                        </frame>
                        <frame *if={SelectedShowItemHelper} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|ItemHelperSelected()|>
                            <label text={SelectedItemHelperLabel} />
                        </frame>
                        <frame *if={SelectedShowPostpone} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|PostponeSelected()|>
                            <label text={#journal.action.postpone} />
                        </frame>
                        <frame *if={SelectedShowCancel} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|RequestCancelSelected()|>
                            <label text={#journal.action.cancel} />
                        </frame>
                        <frame *if={SelectedShowCancelOrder} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|RequestCancelSelectedOrder()|>
                            <label text={#journal.action.cancel} />
                        </frame>
                        </lane>
                    </scrollable>
                </frame>

            </lane>
        </lane>
    </frame>
</lane>
