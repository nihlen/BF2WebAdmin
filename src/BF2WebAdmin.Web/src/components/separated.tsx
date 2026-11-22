import { Children, Fragment } from 'react'

/**
 * Its children with a dot between each two. A child that is not there (null, false) is left out before the dots go in,
 * so a missing part never leaves a dot dangling at the start or the end.
 */
export function Separated({ children }: { children: React.ReactNode }) {
  return Children.toArray(children).map((child, i) => (
    <Fragment key={i}>
      {i > 0 && (
        <span aria-hidden className="text-muted-foreground/50">
          ·
        </span>
      )}
      {child}
    </Fragment>
  ))
}
