import { useState } from 'react'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'

/** A destructive button that asks first. */
export function ConfirmButton({
  title,
  description,
  confirmLabel,
  onConfirm,
  className,
  children,
}: {
  title: string
  description: string
  confirmLabel: string
  onConfirm: () => void
  className?: string
  children: React.ReactNode
}) {
  const [open, setOpen] = useState(false)

  return (
    <AlertDialog open={open} onOpenChange={setOpen}>
      <AlertDialogTrigger render={<Button type="button" variant="destructive" className={className} />}>{children}</AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            onClick={() => {
              setOpen(false)
              onConfirm()
            }}
          >
            {confirmLabel}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
