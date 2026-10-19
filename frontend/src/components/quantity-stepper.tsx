"use client";

import { MinusIcon, PlusIcon } from "lucide-react";
import { Button } from "@/components/ui/button";

interface QuantityStepperProps {
  value: number;
  max: number;
  onChange: (value: number) => void;
  label: string;
}

export function QuantityStepper({ value, max, onChange, label }: QuantityStepperProps) {
  return (
    <div className="flex items-center gap-1" role="group" aria-label={`Quantity for ${label}`}>
      <Button variant="outline" size="icon-sm" onClick={() => onChange(value - 1)} aria-label="Decrease">
        <MinusIcon />
      </Button>
      <span className="w-8 text-center text-sm font-medium tabular-nums" aria-live="polite">
        {value}
      </span>
      <Button
        variant="outline"
        size="icon-sm"
        onClick={() => onChange(value + 1)}
        disabled={value >= max}
        aria-label="Increase"
      >
        <PlusIcon />
      </Button>
    </div>
  );
}
