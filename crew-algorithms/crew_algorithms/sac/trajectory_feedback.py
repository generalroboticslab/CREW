from attrs import field, frozen


@frozen
class TrajectoryFeedback:
    id: int = field(eq=True)
    overall_feedback: str = field(eq=False)
    improvement: str = field(eq=False)
    good: str = field(eq=False)
    rating: int = field(eq=False)

    @classmethod
    def from_id_and_str(cls, id, feedback):
        print(feedback)
        before_overall_feedback, _, after_overall_feedback = feedback.partition(
            "Overall Feedback:"
        )
        before_improvement, _, after_improvement = after_overall_feedback.partition(
            "Improvement:"
        )
        before_good, _, after_good = after_improvement.partition("What went well?:")
        before_rating, _, after_rating = after_good.partition("Rating (1-10):")
        try:
            rating = int(after_rating.strip("n").strip())
        except ValueError:
            rating = 5
        return cls(
            id,
            before_improvement.strip("n").strip(),
            before_good.strip("n").strip(),
            before_rating.strip("n").strip(),
            rating,
        )
