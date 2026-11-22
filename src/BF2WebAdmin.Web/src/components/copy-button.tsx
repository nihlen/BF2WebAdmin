import { Check, Copy } from 'lucide-react'
import { useEffect, useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

/** A small button that copies a value, such as an address or a key, and shows a check for a moment. */
export function CopyButton({ value, label, className }: { value: string; label: string; className?: string }) {
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    if (!copied) return
    const timer = setTimeout(() => setCopied(false), 1500)
    return () => clearTimeout(timer)
  }, [copied])

  return (
    <Button
      type="button"
      variant="ghost"
      size="icon-xs"
      className={cn('text-muted-foreground', className)}
      aria-label={label}
      title={label}
      onClick={() =>
        navigator.clipboard.writeText(value).then(
          () => setCopied(true),
          // Without a secure context (plain http on another host) the browser has no clipboard for the page
          () => toast.error('The browser did not allow copying'),
        )
      }
    >
      {copied ? <Check className="text-emerald-500" /> : <Copy />}
    </Button>
  )
}
