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
} from '@/components/ui/alert-dialog'

export type ConfirmRequest = {
  title: string
  description: React.ReactNode
  confirmLabel: string
  /** Red confirm button, for actions that cannot be undone */
  destructive?: boolean
  onConfirm: () => void
}

/**
 * Asks before an action: confirm({ title, ... }) opens the dialog, and onConfirm runs on the confirm button.
 * Render the returned dialog once.
 */
export function useConfirm() {
  const [request, setRequest] = useState<ConfirmRequest | null>(null)

  const dialog = (
    <AlertDialog open={!!request} onOpenChange={(open) => !open && setRequest(null)}>
      <AlertDialogContent>
        {request && (
          <>
            <AlertDialogHeader>
              <AlertDialogTitle>{request.title}</AlertDialogTitle>
              <AlertDialogDescription>{request.description}</AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction
                variant={request.destructive ? 'destructive' : 'default'}
                onClick={() => {
                  setRequest(null)
                  request.onConfirm()
                }}
              >
                {request.confirmLabel}
              </AlertDialogAction>
            </AlertDialogFooter>
          </>
        )}
      </AlertDialogContent>
    </AlertDialog>
  )

  return { confirm: setRequest, dialog }
}
