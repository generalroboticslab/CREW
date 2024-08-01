namespace Examples.HideAndSeek {
    public enum NetOpCode
    {
        Feedback = 0, // human feedbacks

        GameEpisodeStart = 1,
        GameEpisodeStop = 2,

        HiderHasDied = 3,
        SeekerHasCaught = 4,
    }
}
