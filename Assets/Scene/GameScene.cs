﻿using Assets.Foundation.Constant;
using Assets.Foundation.Model;
using Assets.Foundation.UI.Common;
using Assets.Foundation.UI.PopUp;
using Assets.Object;
using GoogleMobileAds.Api;
using GooglePlayGames;
using Manager;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameScene : MonoBehaviour
{
    private GameManager gameManager;
    private DataManager dataManager;
    private NetworkManager netManager;

    public TextAsset mapJson;
    public TextAsset stageJson;
    public Text roundText;
    public Text chapterNameText;
    public Text stageNameText;
    public Text mapNameText;
    public Image timerGauge;
    public Text gameCodeText;
    public Text noticeText;
    public GameObject noticePanel;
    public Button adButton;

    public Button[] buttons;

    public UIScreenMaskObject screenMaskObj;
    public UIPopUpPanel popUpPanel;
    public MapObject mapObject;
    public HandObject handObject;
    public ScoreObject scoreObject;
    public GameRoomObject gameRoomObject;
    public GameTutorialObject tutorialObject;

    public ChapterModel currentChapter;
    public StageModel currentStage;
    public ScoreViewModel score;

    private Coroutine timerCoroutine;

    private AdRequest adRequest;
    private RewardedAd sponsorAd;
    private BannerView bannerAd;

    [SerializeField]
    private int timerCount;
    public int TimerCount
    {
        get => timerCount;
        set
        {
            timerCount = value;
            timerGauge.fillAmount = timerCount / (float)NumTable.DefaultRoundTime;
        }
    }

    private int roundCount;
    public int RoundCount
    {
        get => roundCount;
        set
        {
            if (value != roundCount)
            {
                roundCount = value;
            }

            onRoundCountChanged?.Invoke(roundCount);
        }
    }

    public string Notice
    {
        get => noticeText.text;
        set
        {
            noticeText.text = value;
            noticePanel.SetActive(!string.IsNullOrEmpty(value));
        }
    }

    public Action<int> onRoundCountChanged;
    private Action onTimeOver;

    private void Awake()
    {
        adRequest = new AdRequest.Builder().Build();
        sponsorAd = new RewardedAd("ca-app-pub-9075521517153750/9316179679");       //  LIVE ID
        //sponsorAd = new RewardedAd("ca-app-pub-3940256099942544/5224354917");     //  TEST ID
        sponsorAd.OnAdClosed += OnAdClosed;
        sponsorAd.OnAdLoaded += OnAdLoaded;
        sponsorAd.OnAdFailedToLoad += OnFailedToLoadAd;
        sponsorAd.OnUserEarnedReward += OnUserEarndReward;

        sponsorAd.LoadAd(adRequest);

        bannerAd = new BannerView("ca-app-pub-9075521517153750/8695672071", AdSize.SmartBanner, AdPosition.Top);
        bannerAd.LoadAd(adRequest);
        bannerAd.Show();

        gameRoomObject.Open();
        Init();
        gameRoomObject.SetGameRoom(gameManager.GameRoom);
    }

    private async void Start()
    {
        while (isActiveAndEnabled)
        {
            var button = await UIButtonAsync.SelectButton<Button>(buttons);
            if ("ExitButton" == button.name)
            {
                await ExitStage();
            }
        }
    }

    private void OnDisable()
    {
        if (null != bannerAd)
        {
            bannerAd.Destroy();
        }

        mapObject.onFixPhaseExit = null;
        onRoundCountChanged = null;
        gameRoomObject.onDisable = null;
        onTimeOver = null;
        scoreObject.onClose = null;
        tutorialObject.onTutorialDone = null;
    }

    private async Task ExitStage()
    {
        var confirm = (UIConfirmPopUp)popUpPanel.Open("Confirm");
        var result = await confirm.GetResult();
        if (true == result)
        {
            GoLobbyScene();
        }
        else
        {
            popUpPanel.TurnOff();
        }
    }

    private async void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SetNextStage();
            MakeStage();
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            screenMaskObj.Toggle();
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            screenMaskObj.SetRect(Input.mousePosition, Vector2.one * 91.5f * Camera.main.orthographicSize);
            screenMaskObj.SetText("A");
        }
#endif

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            await ExitStage();
        }
    }

    private void Init()
    {
        score = new ScoreViewModel();

        popUpPanel.Init();

        SpriteManager.Get();
        gameManager = GameManager.Get();
        dataManager = DataManager.Get();
        netManager = NetworkManager.Get();

        if (ReferenceEquals(null, scoreObject))
        {
            scoreObject = FindObjectOfType<ScoreObject>();
        }
        scoreObject.Init();
        scoreObject.onClose += GoLobbyScene;

        if (ReferenceEquals(null, handObject))
        {
            handObject = FindObjectOfType<HandObject>();
        }
        handObject.Init();

        if (ReferenceEquals(null, mapObject))
        {
            mapObject = FindObjectOfType<MapObject>();
        }
        mapObject.hand = handObject;
        mapObject.Init();
        mapObject.onFixPhaseExit += OnFixPhaseExit;

        onRoundCountChanged = null;
        onRoundCountChanged += OnRoundCountChanged;

        onTimeOver += OnClickFix;

        gameRoomObject.onDisable += StartGame;
    }

    private void StartGame()
    {
        InitChapter();
        MakeStage();
    }

    private void InitChapter()
    {
        if (true == gameManager.IsSoloPlay())
        {
            if (null == gameManager.CurrentStage)
            {
                currentChapter = dataManager.GetFirstChapter();
                currentStage = dataManager.GetFirstStage(currentChapter);
            }
            else
            {
                currentChapter = gameManager.CurrentChapter;
                currentStage = gameManager.CurrentStage;
            }
        }
        else
        {
            currentChapter = dataManager.GetPvpChapter();
            currentStage = dataManager.GetPvpStage(currentChapter);
        }

        gameCodeText.text = gameManager.GameRoom.GameCode;
    }

    public void SetStage(StageModel stage)
    {
        currentStage = stage;
    }

    private void SetNextStage()
    {
        currentStage = dataManager.GetNextStage(currentChapter, currentStage);
        if (currentStage == null)
        {
            currentChapter = dataManager.GetNextChapter(currentChapter);
            currentStage = dataManager.GetFirstStage(currentChapter);
        }

        if (currentStage == null)
        {
            Application.Quit();
        }
    }

    public void MakeStage()
    {
        if (currentStage == null)
        {
            Log.Error("Stage is null");
            return;
        }

        if (true == currentStage.Name.ToLower().Contains("tutorial"))
        {
            GameTutorialObject.IsOn = true;
            tutorialObject.enabled = true;
            tutorialObject.onTutorialDone += () => 
            {
                gameManager.ClearStage(currentStage.Id);
            };
            tutorialObject.onTutorialDone += SetNextStage;
            tutorialObject.onTutorialDone += MakeStage;
            tutorialObject.onTutorialDone += () =>
            {
                GameTutorialObject.IsOn = false;
            };
        }
        else
        {
            tutorialObject.enabled = false;
            mapObject.onClickObject = null;
            handObject.onClickObject = null;
        }

        chapterNameText.text = dataManager.Localize("Chapter", currentChapter?.Name);
        mapNameText.text = currentStage.MapName;
        stageNameText.text = currentStage.Name;

        handObject.stage = currentStage;
        handObject.Ready();

        mapJson = Resources.Load<TextAsset>($"Data/Map/{currentStage.MapName}");
        MapModel map = JObject.Parse(mapJson.text).ToObject<MapModel>();
        mapObject.MakeMap(map);
        mapObject.OpenMap();

        RoundCount = 0;
        GoNextRound();
    }

    public void OnClickCancel()
    {
        mapObject.CancelNode();
    }

    public void OnClickRotate()
    {
        mapObject.Rotate();
    }

    public void OnClickFlip()
    {
        mapObject.Flip();
    }

    public void OnClickFix()
    {
        OnFixPhaseExit();
    }

    public void OnClickRoll()
    {
        handObject.Roll();
    }

    public void OnFixPhaseExit()
    {
        int contructFailCount = mapObject.Fix();
        score.ConstructFailScore += contructFailCount;
        if (RoundCount + 1 > currentStage.Round)
        {
            OnGameOver();
        }
        else
        {
            GoNextRound();
        }
    }

    private void GoNextRound()
    {
        if (null != timerCoroutine)
        {
            StopCoroutine(timerCoroutine);
        }

        if (true == gameManager.IsSoloPlay())
        {
            RoundCount++;
            mapObject.NewRound(RoundCount);
            handObject.Roll();

            timerCoroutine = StartCoroutine(StartTimer());
        }
        else
        {
            string url = UrlTable.GetRoundGameUrl(gameManager.GameRoom.GameCode, RoundCount);
            StartCoroutine(netManager.GetRequestCo(url, OnRoundComplete));
            timerGauge.fillAmount = 1f;
        }
    }

    private void OnRoundComplete(string roundData)
    {
        RoundModel round = RoundModel.Parse(roundData);
        RoundCount = round.Round;
        mapObject.NewRound(RoundCount);
        handObject.Roll(round.Dices);

        timerCoroutine = StartCoroutine(StartTimer());
    }

    private IEnumerator StartTimer()
    {
        if (1 == RoundCount)
        {
            Notice = $"{currentStage.Name}";

            yield return new WaitForSecondsRealtime(1f);

            Notice = $"Round {RoundCount}";
        }

        yield return new WaitForSecondsRealtime(1f);

        Notice = null;

        TimerCount = currentStage.TimePerRound == 0 ? NumTable.DefaultRoundTime : currentStage.TimePerRound;
        while (0 < TimerCount)
        {
            yield return new WaitForSeconds(1f);

            TimerCount--;
        }

        onTimeOver?.Invoke();
    }

    public void OnRoundCountChanged(int round)
    {
        Notice = $"Round {round}";
        roundText.text = $"Round : {round} / {currentStage?.Round}";
    }

    public void OnGameOver()
    {
        adButton.interactable = gameManager.IsRewardAdAvailable();

        onTimeOver = null;
        StopCoroutine(timerCoroutine);
        TimerCount = 0;
        mapObject.GetScore(score);
        score.StageId = currentStage.Id;
        gameManager.ReportScore(score);
#if UNITY_ANDROID
        PlayGamesPlatform.Instance.ReportScore(Math.Abs(score.TotalScore), GPGSIds.leaderboard_highestscore, null);
        PlayGamesPlatform.Instance.ReportProgress(GPGSIds.achievement_stageclear, 100.0f, null);
#endif
        scoreObject.SetScore(score);
        if (false == gameManager.IsSoloPlay())
        {
            scoreObject.SetPvpResult("기다리는 중");
            var game = gameManager.GameRoom;
            string url = UrlTable.GetEndGameUrl(game.GameCode, gameManager.GameUserId, score.TotalScore);
            StartCoroutine(netManager.GetRequestCo(url, (response) =>
            {
                var results = JArray.Parse(response).ToObject<List<GameResultModel>>();
                results = results.OrderByDescending(r => r.Score).ToList();
                var rank = results.FindIndex(r => r.UserId == gameManager.GameUserId);
                scoreObject.SetPvpResult($"{rank + 1} 위");
            }));
        }
        else
        {
            scoreObject.SetPvpResult(string.Empty);
        }

        scoreObject.Open();
    }

    public void GoLobbyScene()
    {
        if (!gameManager.IsSoloPlay())
        {
            string url = UrlTable.GetExitGameUrl(gameManager.GameRoom.GameCode, gameManager.GameUserId);
            netManager.GetRequest(url);
        }

        var loadAsync = SceneManager.LoadSceneAsync("LobbyScene");
        loadAsync.completed += (o) =>
        {
            if (gameManager.IsSoloPlay())
            {
                var find = FindObjectOfType<Assets.UI.Lobby.UILobby>();
                if (!ReferenceEquals(null, find))
                {
                    find.uiSoloPlayPanel.Open();
                }
            }
            loadAsync.allowSceneActivation = true;
        };
    }

    public void OnClickShowAd()
    {
        StartCoroutine(ShowRewardAdCo());
    }

    private IEnumerator ShowRewardAdCo()
    {
        adButton.interactable = false;

        Notice = "광고를 불러오는 중...";

        yield return new WaitForSecondsRealtime(.5f);

        Log.Info("Show Ad");
        sponsorAd.Show();

        Notice = null;
    }

    private IEnumerator Notify(string message, float sec)
    {
        Notice = message;

        yield return new WaitForSecondsRealtime(sec);

        Notice = null;
    }
    private void OnAdClosed(object sender, EventArgs e)
    {
        Log.Info("Closed Ad");
        sponsorAd.LoadAd(adRequest);
    }

    private void OnAdLoaded(object sender, EventArgs arg)
    {
        Log.Info("Loaded Ad");
    }

    private void OnFailedToLoadAd(object sender, AdErrorEventArgs e)
    {
        Log.Info("Failed load Ad");
        StartCoroutine(Notify("광고 불러오기 실패", 2f));
    }

    private void OnUserEarndReward(object sender, Reward e)
    {
        Log.Info("Earn Reward Ad");
        gameManager.AddRewardCount(1);
    }
}
