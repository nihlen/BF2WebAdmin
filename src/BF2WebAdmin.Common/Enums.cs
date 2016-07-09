namespace BF2WebAdmin.Common
{
    public enum Auth
    {
        Moldovan,
        All,
        Trusted,
        Admin,
        God
    }

    public enum GameState
    {
        PreGame,
        Playing,
        EndGame,
        Paused,
        Restart,
        NotConnected
    }

    public enum Rank
    {
        Private,
        PrivateFirstClass,
        LanceCorporal,
        Corporal,
        Sergeant,
        StaffSergeant,
        GunnerySergeant,
        MasterSergeant,
        MasterGunnerySergeant,
        SergeantMajor,
        SergeantMajorOfTheCorps,
        SecondLieutenant,
        FirstLieutenant,
        Captain,
        Major,
        LieutenantColonel,
        Colonel,
        BrigadierGeneral,
        MajorGeneral,
        LieutenantGeneral,
        General
    }

    public enum Medal
    {
        GoldMedal = 2051907,
        SilverMedal = 2051919,
        BronzeMedal = 2051902,
        PurpleHeart = 2191608,
        Unknown = 2190703
    }
}