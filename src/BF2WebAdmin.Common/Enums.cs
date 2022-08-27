namespace BF2WebAdmin.Common;

public enum Auth
{
    Untrusted = 0,
    All = 10,
    Trusted = 20,
    Admin = 50,
    SuperAdmin = 75,
    God = 100,
    None = int.MaxValue
}

public enum Rank
{
    Private = 0,
    PrivateFirstClass = 1,
    LanceCorporal = 2,
    Corporal = 3,
    Sergeant = 4,
    StaffSergeant = 5,
    GunnerySergeant = 6,
    MasterSergeant = 7,
    FirstSergeant = 8,
    MasterGunnerySergeant = 9,
    SergeantMajor = 10,
    SergeantMajorOfTheCorps = 11,
    SecondLieutenant = 12,
    FirstLieutenant = 13,
    Captain = 14,
    Major = 15,
    LieutenantColonel = 16,
    Colonel = 17,
    BrigadierGeneral = 18,
    MajorGeneral = 19,
    LieutenantGeneral = 20,
    General = 21
}

public enum Medal
{
    GoldMedal = 2051907,
    SilverMedal = 2051919,
    BronzeMedal = 2051902,
    PurpleHeart = 2191608,
    Unknown = 2190703
}