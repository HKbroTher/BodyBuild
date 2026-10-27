using System.Collections.Generic;
using MassFactory.Core;
using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>
    /// Immediate-mode HUD for the single gameplay screen: top bar, roster, athlete card, controls,
    /// relic shop, end screens and the title menu. Reads state from the session every frame and sends
    /// requests to <see cref="GameManager"/>; it never changes gameplay state itself.
    /// </summary>
    public sealed class GameplayUIController : MonoBehaviour
    {
        private const float ReferenceHeight = 1080f;
        private const float MinVirtualWidth = 1500f;
        private const float SidePanelWidth = 390f;
        private const float TopBarHeight = 82f;

        private enum Modal
        {
            None,
            Shop,
            ConfirmSacrifice,
            ConfirmSubmit,
            Help
        }

        private static readonly InjectionOutcome[] OddsOrder =
        {
            InjectionOutcome.BodyDestroyed, InjectionOutcome.Atrophy, InjectionOutcome.NoEffect,
            InjectionOutcome.Growth, InjectionOutcome.Mutation
        };

        private readonly UiTheme _theme = new UiTheme();
        private GameManager _game;
        private Modal _modal = Modal.None;
        private float _width;
        private float _height;
        private Vector2 _rosterScroll;
        // Keys currently held down; OS key repeat must never fire a second irreversible action.
        private readonly HashSet<KeyCode> _heldKeys = new HashSet<KeyCode>();

        private void Awake()
        {
            _game = GetComponent<GameManager>();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) _heldKeys.Clear(); // key-up events are lost while unfocused
        }

        private void OnGUI()
        {
            GUI.depth = 0;
            if (!_theme.IsBuilt) _theme.Build();

            float scale = Mathf.Min(Screen.height / ReferenceHeight, Screen.width / MinVirtualWidth);
            if (scale <= 0) return;
            _width = Screen.width / scale;
            _height = Screen.height / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            DrawFlash();

            if (_game.CurrentScreen == GameManager.AppScreen.Title || _game.Session == null)
            {
                DrawTitle();
                DrawTooltip();
                return;
            }

            var session = _game.Session;
            HandleKeys(session);
            DrawTopBar(session);
            DrawRoster(session);
            DrawAthleteCard(session);
            DrawLog();
            DrawFloatingText();
            DrawBanner();

            if (session.IsRunOver && !_game.Busy) DrawEndScreen(session);
            else if (_modal == Modal.Shop) DrawShop(session);
            else if (_modal == Modal.ConfirmSacrifice) DrawConfirm("Sacrifice your last athlete?", "The requirement is not met yet. This ends the run.", _game.Sacrifice);
            else if (_modal == Modal.ConfirmSubmit) DrawConfirm("Submit your last athlete?", "Their score is not enough to meet the requirement. This ends the run.", _game.Submit);
            else if (_modal == Modal.Help) DrawHelp();

            DrawTooltip();
        }

        // ==== Input ============================================================================

        private void HandleKeys(GameSession session)
        {
            var e = Event.current;
            if (e.type == EventType.KeyUp)
            {
                _heldKeys.Remove(e.keyCode);
                return;
            }
            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;
            if (!_heldKeys.Add(e.keyCode))
            {
                e.Use();
                return;
            }

            if (e.keyCode == KeyCode.Escape)
            {
                if (_modal != Modal.None) _modal = Modal.None;
                else _game.ReturnToTitle();
                e.Use();
                return;
            }
            if (e.keyCode == KeyCode.F1)
            {
                _modal = _modal == Modal.Help ? Modal.None : Modal.Help;
                e.Use();
                return;
            }
            if (_modal != Modal.None && _modal != Modal.Shop) return;
            if (session.IsRunOver) return;

            switch (e.keyCode)
            {
                case KeyCode.Space: TryInject(session); break;
                case KeyCode.S: TrySubmit(session); break;
                case KeyCode.X: TrySacrifice(session); break;
                case KeyCode.Tab: _game.SelectNext(); break;
                case KeyCode.R: _game.ActivateRush(); break;
                case KeyCode.B: _modal = _modal == Modal.Shop ? Modal.None : Modal.Shop; break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter: _game.Advance(); break;
                case KeyCode.F: _game.fastAnimations = !_game.fastAnimations; break;
                default: return;
            }
            e.Use();
        }

        private void TryInject(GameSession session)
        {
            if (_modal == Modal.Shop) _modal = Modal.None;
            _game.Inject();
        }

        private void TrySubmit(GameSession session)
        {
            var athlete = session.SelectedAthlete;
            if (athlete == null || _game.Busy) return;
            if (session.SubmitWouldEndRun(athlete)) _modal = Modal.ConfirmSubmit;
            else _game.Submit();
        }

        private void TrySacrifice(GameSession session)
        {
            var athlete = session.SelectedAthlete;
            if (athlete == null || _game.Busy) return;
            if (session.SacrificeWouldEndRun(athlete)) _modal = Modal.ConfirmSacrifice;
            else _game.Sacrifice();
        }

        // ==== HUD ==============================================================================

        private void DrawTopBar(GameSession session)
        {
            var competition = session.Competition;
            var bar = new Rect(0, 0, _width, TopBarHeight);
            _theme.Rect(bar, new Color(0.03f, 0.03f, 0.03f, 0.9f));
            _theme.Rect(new Rect(0, TopBarHeight - 3, _width, 3), UiTheme.Accent);

            GUI.Label(new Rect(20, 6, 420, 24), "ROUND " + competition.RoundNumber + " / " + competition.TotalRounds, _theme.Small);
            _theme.Shadowed(new Rect(20, 28, 520, 44), competition.Current.Name.ToUpperInvariant(), _theme.Header, Color.white);

            // Progress bar
            float barWidth = Mathf.Min(620f, _width - 2 * 560f + 520f);
            var progress = new Rect((_width - barWidth) / 2f, 16, barWidth, 34);
            _theme.Rect(progress, new Color(0.15f, 0.15f, 0.15f));
            var fillColor = competition.ThresholdMet ? new Color(0.25f, 0.8f, 0.3f) : UiTheme.Accent;
            _theme.Rect(new Rect(progress.x, progress.y, progress.width * competition.Progress, progress.height), fillColor);
            GUI.Label(progress, "<b>" + competition.SubmittedScore.ToString("N0") + " / " + competition.RequiredScore.ToString("N0") + "</b>", _theme.Center);
            GUI.Label(new Rect(progress.x, progress.yMax + 2, progress.width, 24),
                competition.ThresholdMet ? "REQUIREMENT MET — advance when ready" : "Need " + competition.RemainingScore.ToString("N0") + " more",
                CenterSmall());

            // Resources
            float right = _width - 20;
            DrawStat(new Rect(right - 180, 10, 180, 62), "RESEARCH", session.State.Research.ToString(), UiTheme.Research);
            DrawStat(new Rect(right - 380, 10, 180, 62), "ATHLETES",
                session.ActiveAthleteCount + " / " + session.Config.Roster.RosterCapacity, Color.white);
        }

        private void DrawStat(Rect rect, string label, string value, Color color)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 22), label, _theme.Small);
            _theme.Shadowed(new Rect(rect.x, rect.y + 20, rect.width, 40), value, _theme.Header, color);
        }

        private void DrawRoster(GameSession session)
        {
            var panel = new Rect(16, TopBarHeight + 14, SidePanelWidth, _height - TopBarHeight - 200);
            _theme.Panel(panel);
            GUI.Label(new Rect(panel.x + 14, panel.y + 10, panel.width - 28, 30), "<b>ROSTER</b>", _theme.Label);

            float itemHeight = 62f;
            float listHeight = Mathf.Min(session.Roster.Count * (itemHeight + 6), panel.height * 0.55f);
            var view = new Rect(panel.x + 10, panel.y + 44, panel.width - 20, listHeight);
            var content = new Rect(0, 0, view.width - 18, session.Roster.Count * (itemHeight + 6));
            _rosterScroll = GUI.BeginScrollView(view, _rosterScroll, content);
            for (int i = 0; i < session.Roster.Count; i++)
            {
                var athlete = session.Roster[i];
                bool selected = athlete.AthleteID == session.State.SelectedAthleteId;
                double risk = session.PreviewDestroyChance(athlete);
                string text = "<b>" + Truncate(athlete.DisplayName, 30) + "</b>\nLV " + athlete.MassLevel + "   " +
                              athlete.CurrentScore.ToString("N0") + "   <color=#" + UiTheme.Hex(RiskColor(risk)) + ">" +
                              risk.ToString("0") + "% risk</color>";
                GUI.enabled = !_game.Busy;
                if (GUI.Button(new Rect(0, i * (itemHeight + 6), content.width, itemHeight), text,
                        selected ? _theme.ListItemSelected : _theme.ListItem))
                    _game.SelectAthlete(athlete.AthleteID);
                GUI.enabled = true;
            }
            GUI.EndScrollView();
            if (session.Roster.Count == 0)
                GUI.Label(new Rect(view.x + 4, view.y, view.width, 60), "No athletes left.", _theme.Small);

            // Relics
            float y = view.yMax + 18;
            GUI.Label(new Rect(panel.x + 14, y, panel.width - 28, 30),
                "<b>RELICS " + session.Relics.Count + " / " + session.Config.Economy.MaxRelics + "</b>", _theme.Label);
            y += 32;
            foreach (var relic in session.Relics.Equipped)
            {
                if (y > panel.yMax - 28) break;
                GUI.Label(new Rect(panel.x + 18, y, panel.width - 30, 24),
                    new GUIContent("• " + relic.DisplayName, relic.Description), _theme.Small);
                y += 24;
            }
            if (session.Relics.Count == 0)
                GUI.Label(new Rect(panel.x + 18, y, panel.width - 30, 48), "None yet. Sacrifice athletes or win competitions for research.", _theme.Small);
        }

        private void DrawAthleteCard(GameSession session)
        {
            var panel = new Rect(_width - SidePanelWidth - 16, TopBarHeight + 14, SidePanelWidth, _height - TopBarHeight - 30);
            _theme.Panel(panel);
            float x = panel.x + 16, w = panel.width - 32, y = panel.y + 12;

            var athlete = _game.Busy && _game.PresentedAthlete != null ? _game.PresentedAthlete : session.SelectedAthlete;
            bool presenting = _game.Busy && _game.PresentedAthlete != null;

            if (athlete != null)
            {
                int level = presenting ? _game.DisplayedLevel : athlete.MassLevel;
                long score = presenting ? _game.DisplayedScore : athlete.CurrentScore;
                bool alive = athlete.IsAlive || !presenting;

                GUI.Label(new Rect(x, y, w, 50), "<b>" + athlete.DisplayName + "</b>", _theme.Label);
                y += 52;
                GUI.Label(new Rect(x, y, w, 22), alive ? MuscleProfile.TierName(level).ToUpperInvariant() : "DECEASED", _theme.Small);
                y += 26;

                GUI.Label(new Rect(x, y, w / 2, 22), "MASS LEVEL", _theme.Small);
                GUI.Label(new Rect(x + w / 2, y, w / 2, 22), "SCORE", _theme.Small);
                y += 20;
                _theme.Shadowed(new Rect(x, y, w / 2, 52), level.ToString(), _theme.Big, Color.white);
                _theme.Shadowed(new Rect(x + w / 2, y, w / 2, 52), alive ? FormatScore(score) : "0", _theme.Big, UiTheme.Gold);
                y += 58;

                if (athlete.IsActive && !presenting)
                {
                    var odds = session.PreviewOutcomes(athlete);
                    double risk = odds[InjectionOutcome.BodyDestroyed];
                    GUI.Label(new Rect(x, y, w, 22), "DESTRUCTION CHANCE (NEXT INJECTION)", _theme.Small);
                    y += 20;
                    _theme.Shadowed(new Rect(x, y, w, 52), risk.ToString("0.#") + "%", _theme.Big, RiskColor(risk));
                    y += 54;
                    if (session.Relics.PreventsNextDestruction(athlete) && risk > 0)
                    {
                        GUI.Label(new Rect(x, y, w, 22), "<color=#" + UiTheme.Hex(UiTheme.Gold) + ">Second Heart will absorb one destruction</color>", _theme.Small);
                        y += 22;
                    }
                    DrawOdds(new Rect(x, y, w, 70), odds);
                    y += 74;
                    GUI.Label(new Rect(x, y, w, 22), "Injections: " + athlete.InjectionCount +
                                                     "    Research if sacrificed: " + session.PreviewSacrificeResearch(athlete), _theme.Small);
                    y += 28;
                }
                else
                {
                    y += 150;
                }
            }
            else
            {
                GUI.Label(new Rect(x, y, w, 60), "No athlete selected.", _theme.Label);
                y += 230;
            }

            DrawRush(session, new Rect(x, y, w, 40));
            y += 48;
            DrawControls(session, new Rect(x, y, w, panel.yMax - y - 12));
        }

        private void DrawOdds(Rect rect, OutcomeTable odds)
        {
            var bar = new Rect(rect.x, rect.y, rect.width, 16);
            float cursor = bar.x;
            foreach (var outcome in OddsOrder)
            {
                float width = bar.width * (float)(odds[outcome] / 100.0);
                if (width <= 0) continue;
                _theme.Rect(new Rect(cursor, bar.y, width, bar.height), InjectionSequencePlayer.ColorFor(outcome));
                cursor += width;
            }
            float column = rect.width / 3f;
            for (int i = 0; i < OddsOrder.Length; i++)
            {
                var outcome = OddsOrder[i];
                var cell = new Rect(rect.x + (i % 3) * column, rect.y + 22 + (i / 3) * 22, column, 22);
                GUI.Label(cell, "<color=#" + UiTheme.Hex(InjectionSequencePlayer.ColorFor(outcome)) + ">" +
                                ShortLabel(outcome) + " " + odds[outcome].ToString("0.#") + "%</color>", _theme.Small);
            }
        }

        private void DrawRush(GameSession session, Rect rect)
        {
            var rush = session.Rush;
            if (!rush.Enabled) return;
            _theme.Rect(rect, new Color(0.12f, 0.1f, 0.02f));
            float fill = rush.IsActive ? rush.RemainingInjections / (float)Mathf.Max(1, rush.Duration) : rush.Energy / (float)Mathf.Max(1, rush.Threshold);
            _theme.Rect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height),
                rush.IsActive ? InjectionSequencePlayer.RushColor : new Color(0.55f, 0.45f, 0.05f));
            string label = rush.IsActive
                ? "<b>ANABOLIC RUSH ACTIVE — " + rush.RemainingInjections + " SAFE INJECTIONS</b>"
                : "ANABOLIC RUSH " + rush.Energy + " / " + rush.Threshold;
            GUI.Label(rect, label, _theme.Center);
        }

        private void DrawControls(GameSession session, Rect rect)
        {
            var athlete = session.SelectedAthlete;
            bool idle = !_game.Busy && !session.IsRunOver;
            float y = rect.y;
            float w = rect.width;

            string injectWhy = session.WhyCannotInject(athlete);
            if (ControlButton(new Rect(rect.x, y, w, 74), "INJECT  [Space]", idle && injectWhy == null, injectWhy, _theme.BigButton))
                TryInject(session);
            y += 82;

            if (session.Rush.Enabled)
            {
                bool rushReady = idle && session.Rush.CanActivate;
                if (ControlButton(new Rect(rect.x, y, w, 44), "ACTIVATE RUSH  [R]", rushReady,
                        session.Rush.IsActive ? "Already active." : "Charge by landing GROWTH and MUTATION.", _theme.Button))
                    _game.ActivateRush();
                y += 52;
            }

            string submitLabel = athlete != null ? "SUBMIT  +" + athlete.CurrentScore.ToString("N0") + "  [S]" : "SUBMIT  [S]";
            string submitWhy = session.WhyCannotSubmit(athlete);
            if (ControlButton(new Rect(rect.x, y, w, 50), submitLabel, idle && submitWhy == null, submitWhy, _theme.Button))
                TrySubmit(session);
            y += 58;

            string sacrificeLabel = athlete != null ? "SACRIFICE  +" + session.PreviewSacrificeResearch(athlete) + " RESEARCH  [X]" : "SACRIFICE  [X]";
            string sacrificeWhy = session.WhyCannotSacrifice(athlete);
            if (ControlButton(new Rect(rect.x, y, w, 50), sacrificeLabel, idle && sacrificeWhy == null, sacrificeWhy, _theme.Button))
                TrySacrifice(session);
            y += 58;

            float half = (w - 8) / 2f;
            if (ControlButton(new Rect(rect.x, y, half, 46), "NEXT  [Tab]", idle && session.CanSelectNext, "No other athlete.", _theme.Button))
                _game.SelectNext();
            if (ControlButton(new Rect(rect.x + half + 8, y, half, 46), "SHOP  [B]", idle, null, _theme.Button))
                _modal = Modal.Shop;
            y += 54;

            string advanceWhy = session.WhyCannotAdvance();
            string advanceLabel = session.Competition.IsFinalRound ? "CLAIM THE CROWN  [Enter]" : "ADVANCE ROUND  [Enter]";
            if (ControlButton(new Rect(rect.x, y, w, 54), advanceLabel, idle && advanceWhy == null, advanceWhy,
                    session.Competition.ThresholdMet ? _theme.BigButton : _theme.Button))
                _game.Advance();
            y += 62;

            GUI.Label(new Rect(rect.x, y, w, 22), "F1 help   F fast animations " + (_game.fastAnimations ? "ON" : "OFF") + "   Esc menu", _theme.Small);
        }

        private bool ControlButton(Rect rect, string text, bool enabled, string disabledReason, GUIStyle style)
        {
            bool previous = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(rect, new GUIContent(text, enabled ? null : disabledReason), style);
            GUI.enabled = previous;
            return clicked && enabled;
        }

        private void DrawLog()
        {
            var log = _game.Feedback.Log;
            float width = Mathf.Min(760f, _width - 2 * (SidePanelWidth + 40));
            var rect = new Rect((_width - width) / 2f, _height - 30 - log.Count * 24, width, log.Count * 24 + 10);
            for (int i = 0; i < log.Count; i++)
            {
                float alpha = 0.45f + 0.55f * (i + 1) / log.Count;
                _theme.Shadowed(new Rect(rect.x, rect.y + i * 24, rect.width, 24), log[i], CenterSmall(), new Color(1, 1, 1, alpha));
            }
        }

        private void DrawFloatingText()
        {
            var style = _theme.Floating;
            foreach (var text in _game.Feedback.Floating)
            {
                float t = text.Age / text.Lifetime;
                float pop = 1f + 0.45f * (1f - Mathf.Clamp01(text.Age / 0.15f));
                float alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                style.fontSize = Mathf.RoundToInt(text.Size * pop);
                float y = _height * 0.27f + text.YOffset - text.Age * 30f;
                var c = text.Color;
                _theme.Shadowed(new Rect(0, y, _width, text.Size * 1.8f), text.Text, style, new Color(c.r, c.g, c.b, alpha));
            }
        }

        private void DrawBanner()
        {
            var feedback = _game.Feedback;
            if (feedback.BannerTime <= 0) return;
            float alpha = Mathf.Clamp01(feedback.BannerTime / 0.5f);
            var rect = new Rect(0, TopBarHeight + 60, _width, 110);
            _theme.Rect(new Rect((_width - 900) / 2f, rect.y, 900, rect.height), new Color(0, 0, 0, 0.7f * alpha));
            _theme.Floating.fontSize = 46;
            _theme.Shadowed(new Rect(0, rect.y + 8, _width, 56), feedback.Banner, _theme.Floating, new Color(1f, 0.3f, 0.2f, alpha));
            _theme.Floating.fontSize = 24;
            _theme.Shadowed(new Rect(0, rect.y + 64, _width, 36), feedback.BannerSub, _theme.Floating, new Color(1, 1, 1, alpha));
        }

        private void DrawFlash()
        {
            var feedback = _game.Feedback;
            if (feedback.FlashAlpha <= 0) return;
            var c = feedback.FlashColor;
            _theme.Rect(new Rect(0, 0, _width, _height), new Color(c.r, c.g, c.b, feedback.FlashAlpha));
        }

        // ==== Modals ===========================================================================

        private void DrawShop(GameSession session)
        {
            Dim();
            var window = new Rect((_width - 1140) / 2f, 150, 1140, 560);
            _theme.Panel(window);
            GUI.Label(new Rect(window.x + 24, window.y + 16, 700, 40), "<b>BLACK MARKET RELICS</b>", _theme.Header);
            GUI.Label(new Rect(window.x + 24, window.y + 54, 900, 26),
                "One purchase per offering. Research: <color=#" + UiTheme.Hex(UiTheme.Research) + ">" + session.State.Research +
                "</color>   Slots: " + session.Relics.Count + " / " + session.Config.Economy.MaxRelics, _theme.Small);

            var offer = session.Shop.CurrentOffer();
            float cardWidth = 350, gap = 21;
            for (int i = 0; i < offer.Count; i++)
            {
                var relic = offer[i];
                var card = new Rect(window.x + 24 + i * (cardWidth + gap), window.y + 96, cardWidth, 360);
                _theme.Rect(card, new Color(0.12f, 0.12f, 0.12f, 0.95f));
                GUI.Label(new Rect(card.x + 14, card.y + 12, card.width - 28, 34), "<b>" + relic.DisplayName + "</b>", _theme.Label);
                GUI.Label(new Rect(card.x + 14, card.y + 50, card.width - 28, 120), relic.Description, _theme.Label);
                GUI.Label(new Rect(card.x + 14, card.y + 180, card.width - 28, 90), "<i>" + relic.FlavorText + "</i>", _theme.Small);
                string why = session.Shop.WhyCannotBuy(i);
                string label = "BUY  " + session.Shop.PriceOf(relic) + " RESEARCH";
                if (ControlButton(new Rect(card.x + 14, card.yMax - 64, card.width - 28, 50), label, !_game.Busy && why == null, why, _theme.BigButton))
                    _game.BuyRelic(i);
            }
            if (offer.Count == 0)
                GUI.Label(new Rect(window.x + 24, window.y + 120, 600, 30), "Nothing left on the shelves.", _theme.Label);

            string restockWhy = session.Shop.WhyCannotRestock();
            if (ControlButton(new Rect(window.x + 24, window.yMax - 76, 360, 52), "RESTOCK  " + session.Shop.RestockPrice() + " RESEARCH",
                    !_game.Busy && restockWhy == null, restockWhy, _theme.Button))
                _game.RestockShop();
            if (GUI.Button(new Rect(window.xMax - 224, window.yMax - 76, 200, 52), "CLOSE  [B]", _theme.Button))
                _modal = Modal.None;
        }

        private void DrawConfirm(string title, string body, System.Action confirm)
        {
            Dim();
            var window = new Rect((_width - 640) / 2f, 330, 640, 260);
            _theme.Panel(window);
            GUI.Label(new Rect(window.x + 24, window.y + 20, window.width - 48, 40), "<b>" + title + "</b>", _theme.Header);
            GUI.Label(new Rect(window.x + 24, window.y + 70, window.width - 48, 80), body, _theme.Label);
            if (GUI.Button(new Rect(window.x + 24, window.yMax - 76, 280, 52), "DO IT", _theme.BigButton))
            {
                _modal = Modal.None;
                confirm();
            }
            if (GUI.Button(new Rect(window.xMax - 304, window.yMax - 76, 280, 52), "CANCEL", _theme.Button))
                _modal = Modal.None;
        }

        private void DrawHelp()
        {
            Dim();
            var window = new Rect((_width - 900) / 2f, 140, 900, 640);
            _theme.Panel(window);
            GUI.Label(new Rect(window.x + 24, window.y + 16, 800, 40), "<b>HOW TO RUN A MASS FACTORY</b>", _theme.Header);
            const string text =
                "• INJECT to roll one outcome: MUTATION +2, GROWTH +1, NO EFFECT, ATROPHY -1 or BODY DESTROYED.\n" +
                "• Score = 500 x 1.5^MassLevel. The destruction chance climbs with every level.\n" +
                "• SUBMIT sends the athlete to the competition for their score. Reach the requirement, then ADVANCE.\n" +
                "• SACRIFICE converts an athlete into research (score / 500). Spend research on relics in the SHOP.\n" +
                "• Unused athletes stay on the roster for the next round (roster capacity applies).\n" +
                "• Successful injections charge ANABOLIC RUSH: while active, nothing can destroy your athletes.\n" +
                "• The run ends when you have no athletes left and the requirement is not met.\n\n" +
                "Keys: Space inject · S submit · X sacrifice · Tab next athlete · R rush · B shop · Enter advance · F fast animations · Esc menu\n\n" +
                "All substances and side effects are fictional. This is a cartoon about bad decisions.";
            GUI.Label(new Rect(window.x + 24, window.y + 70, window.width - 48, 480), text, _theme.Label);
            if (GUI.Button(new Rect(window.xMax - 224, window.yMax - 76, 200, 52), "CLOSE", _theme.Button))
                _modal = Modal.None;
        }

        private void DrawEndScreen(GameSession session)
        {
            Dim();
            bool victory = session.Phase == RunPhase.Victory;
            var window = new Rect((_width - 820) / 2f, 160, 820, 640);
            _theme.Panel(window);
            _theme.Floating.fontSize = 72;
            _theme.Shadowed(new Rect(window.x, window.y + 24, window.width, 90), victory ? "MR. MUTATION" : "FACTORY CLOSED",
                _theme.Floating, victory ? UiTheme.Gold : UiTheme.Accent);
            GUI.Label(new Rect(window.x + 40, window.y + 120, window.width - 80, 60), session.State.EndReason, CenterLabel());

            var stats = session.State.Stats;
            var lines = new List<string>
            {
                "Competitions won: " + stats.RoundsCompleted + " / " + session.Competition.TotalRounds,
                "Injections: " + stats.Injections + "   (mutations " + stats.Mutations + ", growths " + stats.Growths + ")",
                "Athletes recruited: " + stats.Recruited + "   destroyed: " + stats.Destroyed,
                "Submitted: " + stats.Submitted + "   sacrificed: " + stats.Sacrificed,
                "Best submission: " + stats.BestSubmittedScore.ToString("N0") + "   peak MassLevel: " + stats.MaxMassLevel,
                "Research earned: " + stats.ResearchEarned + "   rushes: " + stats.RushesActivated,
                "Seed: " + session.State.Seed
            };
            GUI.Label(new Rect(window.x + 80, window.y + 200, window.width - 160, 300), string.Join("\n", lines.ToArray()), _theme.Label);

            if (GUI.Button(new Rect(window.x + 60, window.yMax - 96, 330, 64), "NEW RUN", _theme.BigButton))
                _game.NewRun();
            if (GUI.Button(new Rect(window.xMax - 390, window.yMax - 96, 330, 64), "TITLE", _theme.Button))
                _game.ReturnToTitle();
        }

        private void DrawTitle()
        {
            _theme.Rect(new Rect(0, 0, _width, _height), new Color(0, 0, 0, 0.45f));
            _theme.Floating.fontSize = 120;
            _theme.Shadowed(new Rect(0, 150, _width, 150), "MASS FACTORY", _theme.Floating, UiTheme.Accent);
            _theme.Floating.fontSize = 28;
            _theme.Shadowed(new Rect(0, 300, _width, 40), "An underground bodybuilding roguelite. Inject. Pray. Repeat.", _theme.Floating, Color.white);

            float x = (_width - 440) / 2f, y = 400;
            if (_game.HasSave)
            {
                if (GUI.Button(new Rect(x, y, 440, 70), "CONTINUE RUN", _theme.BigButton)) _game.ContinueRun();
                y += 84;
            }
            if (GUI.Button(new Rect(x, y, 440, 70), _game.HasSave ? "NEW RUN (overwrites save)" : "NEW RUN", _game.HasSave ? _theme.Button : _theme.BigButton))
                _game.NewRun();
            y += 90;

            _game.fastAnimations = GUI.Toggle(new Rect(x, y, 440, 32), _game.fastAnimations, "  Fast animations", ToggleStyle());
            y += 40;
            bool retro = GUI.Toggle(new Rect(x, y, 440, 32), _game.retroFilter, "  PS2 pixel filter", ToggleStyle());
            if (retro != _game.retroFilter) _game.SetRetro(retro);
            y += 56;

            if (!Application.isEditor && GUI.Button(new Rect(x, y, 440, 56), "QUIT", _theme.Button)) Application.Quit();

            GUI.Label(new Rect(0, _height - 60, _width, 30),
                "All substances, side effects and biology in this game are fictional.", CenterSmall());
        }

        private void DrawTooltip()
        {
            if (string.IsNullOrEmpty(GUI.tooltip)) return;
            var mouse = Event.current.mousePosition;
            var content = new GUIContent(GUI.tooltip);
            float width = Mathf.Min(420f, _theme.Tooltip.CalcSize(content).x + 4);
            float height = _theme.Tooltip.CalcHeight(content, width);
            float x = Mathf.Min(mouse.x + 18, _width - width - 8);
            float y = Mathf.Min(mouse.y + 18, _height - height - 8);
            GUI.Label(new Rect(x, y, width, height), content, _theme.Tooltip);
        }

        // ==== Helpers ==========================================================================

        private void Dim()
        {
            _theme.Rect(new Rect(0, 0, _width, _height), new Color(0, 0, 0, 0.6f));
        }

        private GUIStyle _centerSmall;
        private GUIStyle _centerLabel;
        private GUIStyle _toggle;

        private GUIStyle CenterSmall()
        {
            return _centerSmall ?? (_centerSmall = new GUIStyle(_theme.Small) { alignment = TextAnchor.MiddleCenter, wordWrap = false });
        }

        private GUIStyle CenterLabel()
        {
            return _centerLabel ?? (_centerLabel = new GUIStyle(_theme.Label) { alignment = TextAnchor.MiddleCenter });
        }

        private GUIStyle ToggleStyle()
        {
            if (_toggle != null) return _toggle;
            _toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 22 };
            _toggle.normal.textColor = Color.white;
            _toggle.onNormal.textColor = Color.white;
            _toggle.hover.textColor = UiTheme.Gold;
            _toggle.onHover.textColor = UiTheme.Gold;
            return _toggle;
        }

        private static Color RiskColor(double risk)
        {
            if (risk <= 0) return new Color(0.4f, 1f, 0.4f);
            if (risk < 15) return new Color(1f, 0.9f, 0.4f);
            if (risk < 30) return new Color(1f, 0.55f, 0.2f);
            return new Color(1f, 0.2f, 0.15f);
        }

        private static string ShortLabel(InjectionOutcome outcome)
        {
            switch (outcome)
            {
                case InjectionOutcome.BodyDestroyed: return "DESTROY";
                case InjectionOutcome.Atrophy: return "ATROPHY";
                case InjectionOutcome.NoEffect: return "NOTHING";
                case InjectionOutcome.Growth: return "GROWTH";
                default: return "MUTATION";
            }
        }

        private static string FormatScore(long score)
        {
            if (score >= 10_000_000) return (score / 1_000_000.0).ToString("0.0") + "M";
            return score.ToString("N0");
        }

        private static string Truncate(string text, int length)
        {
            return text.Length <= length ? text : text.Substring(0, length - 1) + "…";
        }
    }
}
