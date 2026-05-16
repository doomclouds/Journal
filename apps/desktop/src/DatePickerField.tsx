import { CalendarDays } from "lucide-react";

type DatePickerFieldProps = {
  id?: string;
  label: string;
  ariaLabel?: string;
  value: string;
  className?: string;
  showLabel?: boolean;
  onChange: (value: string) => void;
};

export function DatePickerField({
  id,
  label,
  ariaLabel,
  value,
  className = "",
  showLabel = false,
  onChange
}: DatePickerFieldProps) {
  const classes = ["date-picker-field", showLabel ? "date-picker-field-with-label" : "", className]
    .filter(Boolean)
    .join(" ");

  return (
    <label className={classes} htmlFor={id}>
      <CalendarDays className="date-picker-icon" size={15} aria-hidden="true" />
      {showLabel ? <span className="date-picker-label">{label}</span> : null}
      <input
        id={id}
        aria-label={ariaLabel ?? label}
        type="date"
        value={value}
        onChange={event => onChange(event.target.value)}
      />
    </label>
  );
}
