using JuegoCriminal.Services;
using UnityEditor;
using UnityEngine;

// This entire file is in an Editor-only assembly, never in a player build.
public static class DevelopmentEconomyTools
{
    const string BalanceMenu="Tools/Criminal Game/Desarrollo/Saldo a 5.000.000 (Play)";
    public const int TestBalance=5000000;
    public static bool SetBalance(EconomyService economy,int target)
    {
        var save=economy?economy.GetComponent<SaveService>():null;
        if(!save || !save.HasCurrentGame)return false;
        target=Mathf.Max(0,target);
        if(economy.Money<target)economy.AddMoney(target-economy.Money);
        else if(economy.Money>target && !economy.TrySpend(economy.Money-target))return false;
        return economy.Money==target;
    }
    [MenuItem(BalanceMenu)]
    public static void GrantTestBalance()
    {
        if(!EditorApplication.isPlaying)return;
        if(SetBalance(Object.FindAnyObjectByType<EconomyService>(),TestBalance))
            Debug.Log("Development balance set to $5,000,000. Save the game normally only if you want to keep this balance.");
        else Debug.LogWarning("Start or load a game before setting the test balance.");
    }
    [MenuItem(BalanceMenu,true)]
    static bool CanGrant()
    {
        if(!EditorApplication.isPlaying)return false;
        var economy=Object.FindAnyObjectByType<EconomyService>();var save=economy?economy.GetComponent<SaveService>():null;
        return save && save.HasCurrentGame;
    }
}
